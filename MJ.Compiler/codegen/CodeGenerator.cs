using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using LLVMSharp;

using mj.compiler.main;
using mj.compiler.symbol;
using mj.compiler.tree;
using mj.compiler.utils;

using static mj.compiler.codegen.LLVMUtils;

using Type = mj.compiler.symbol.Type;

namespace mj.compiler.codegen
{
    public class CodeGenerator : AstVisitor<LLVMValueRef>
    {
        private static readonly Context.Key<CodeGenerator> CONTEXT_KEY = new Context.Key<CodeGenerator>();

        public static CodeGenerator instance(Context ctx) =>
            ctx.tryGet(CONTEXT_KEY, out var instance) ? instance : new CodeGenerator(ctx);

        private readonly LLVMTypeResolver typeResolver;
        private VariableAllocator variableAllocator;
        private readonly Typings typings;
        private readonly CommandLineOptions options;
        private readonly Symtab symtab;

        private readonly TargetOS targetOS;

        public CodeGenerator(Context ctx)
        {
            ctx.put(CONTEXT_KEY, this);

            typings = Typings.instance(ctx);
            typeResolver = new LLVMTypeResolver();
            options = CommandLineOptions.instance(ctx);
            symtab = Symtab.instance(ctx);

            targetOS = getTargetOS();
        }

        private LLVMContextRef context;
        private LLVMModuleRef module;
        private LLVMValueRef function;
        private MethodDef method;
        private LLVMBuilderRef builder;

        private LLVMValueRef initRuntime;
        private LLVMValueRef allocFunction;
        private LLVMValueRef allocArrayFunction;

        private static readonly LLVMValueRef nullValue = default(LLVMValueRef);

        private readonly LLVMValueRef dummyNullPtr = LLVM.ConstPointerNull(PTR_INT8);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int MainFunction(int argc, long argvPtr);

        public void main(IList<CompilationUnit> trees)
        {
            if (trees.Count == 0) {
                return;
            }

            LLVMPassManagerRef passManager = LLVM.CreatePassManager();
            LLVM.AddStripDeadPrototypesPass(passManager);
            LLVM.AddPromoteMemoryToRegisterPass(passManager);
            CompilerNative.AddRewriteStatepointsForGCPass(passManager);
            LLVM.AddEarlyCSEPass(passManager);
            LLVM.AddCFGSimplificationPass(passManager);
            LLVM.AddNewGVNPass(passManager);
            LLVM.AddLateCFGSimplificationPass(passManager);

            context = LLVM.GetGlobalContext();
            module = LLVM.ModuleCreateWithNameInContext("TheProgram", context);
            builder = context.CreateBuilderInContext();

            declareBuiltins();
            declareRuntimeHelpers();

            variableAllocator = new VariableAllocator(builder, typeResolver);

            build(trees);

            if (options.DumpIR) {
                dumpIR();
            }

            LLVMBool success = new LLVMBool(0);

            if (LLVM.VerifyModule(module, LLVMVerifierFailureAction.LLVMPrintMessageAction, out var error) != success) {
                Console.WriteLine($"Verfy Error: {error}");
                return;
            }

            LLVM.RunPassManager(passManager, module);

            if (options.DumpIR) {
                dumpIR();
            }

            if (options.Execute) {
                mcJit();
            } else {
                makeObjectFile();
            }

            LLVM.DisposePassManager(passManager);
        }

        private void dumpIR()
        {
            // LLVM.DumpModule() is unreliable on Windows. It sometimes prints binary garbage.
            IntPtr stringPtr = LLVM.PrintModuleToString(module);
            string str = Marshal.PtrToStringUTF8(stringPtr);
            Console.WriteLine(str);
        }

        private void makeObjectFile()
        {
            LLVM.InitializeX86TargetMC();
            LLVM.InitializeX86Target();
            LLVM.InitializeX86TargetInfo();
            LLVM.InitializeX86AsmParser();
            LLVM.InitializeX86AsmPrinter();

            LLVMTargetRef target = LLVM.GetFirstTarget();
            if (target.Pointer == IntPtr.Zero) {
                return;
            }

            string triple = getTargetTriple();

            LLVMTargetMachineRef targetMachine = LLVM.CreateTargetMachine(target, triple, "generic", "",
                LLVMCodeGenOptLevel.LLVMCodeGenLevelDefault, LLVMRelocMode.LLVMRelocDefault,
                LLVMCodeModel.LLVMCodeModelDefault);

            LLVM.TargetMachineEmitToMemoryBuffer(targetMachine, module, LLVMCodeGenFileType.LLVMObjectFile,
                out var _, out var memoryBuffer);

            size_t bufferSize = LLVM.GetBufferSize(memoryBuffer);
            IntPtr bufferStart = LLVM.GetBufferStart(memoryBuffer);

            unsafe {
                Stream s = new UnmanagedMemoryStream((byte*)bufferStart.ToPointer(), bufferSize);
                FileInfo file = new FileInfo(options.OutPath);
                if (file.Exists) {
                    file.Delete();
                }

                using (FileStream fs = file.Open(FileMode.Create, FileAccess.Write))
                using (DisposableBuffer unused = new DisposableBuffer(memoryBuffer)) {
                    s.CopyTo(fs);
                }
            }
        }

        private string getTargetTriple()
        {
            string triple;
            switch (targetOS) {
                case TargetOS.Windows:
                    triple = "x86_64-unknown-win32";
                    break;
                case TargetOS.OSX:
                    triple = "x86_64-apple-darwin";
                    break;
                case TargetOS.Linux:
                    triple = "x86_64-unknown-linux"; // ???
                    break;
                default:
                    throw new ArgumentOutOfRangeException("OSPlatform unknown");
            }
            return triple;
        }

        // C++ RAII style object to wrap the unmanaged resource
        private struct DisposableBuffer : IDisposable
        {
            private readonly LLVMMemoryBufferRef buffer;
            public DisposableBuffer(LLVMMemoryBufferRef buffer) => this.buffer = buffer;
            public void Dispose() => LLVM.DisposeMemoryBuffer(buffer);
        }

        private void mcJit()
        {
            loadRtLib();
            LLVMBool success = new LLVMBool(0);
            LLVM.LinkInMCJIT();

            LLVM.InitializeX86TargetMC();
            LLVM.InitializeX86Target();
            LLVM.InitializeX86TargetInfo();
            LLVM.InitializeX86AsmParser();
            LLVM.InitializeX86AsmPrinter();

            LLVMMCJITCompilerOptions jitOptions = new LLVMMCJITCompilerOptions {
                NoFramePointerElim = 1,
                OptLevel = 0
            };

            LLVM.InitializeMCJITCompilerOptions(jitOptions);
            if (LLVM.CreateMCJITCompilerForModule(out var engine, module, jitOptions, out var error) != success) {
                Console.WriteLine($"Error: {error}");
                return;
            }

            LLVMValueRef mainFunction = LLVM.GetNamedFunction(module, "main");
            var execute = (MainFunction)Marshal.GetDelegateForFunctionPointer(
                LLVM.GetPointerToGlobal(engine, mainFunction), typeof(MainFunction));

            int result = execute(0, 0);

            Console.WriteLine($"Program excuted with result: {result}");

            LLVM.DisposeBuilder(builder);
            LLVM.DisposeExecutionEngine(engine);
        }

        private void loadRtLib()
        {
            String libPath;
            switch (targetOS) {
                case TargetOS.Windows:
                    libPath = "./mj_rt.dll";
                    break;
                case TargetOS.OSX:
                    libPath = "./libmj_rt.dylib";
                    break;
                case TargetOS.Linux:
                    libPath = "./libmj_rt.so";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            LLVM.LoadLibraryPermanently(libPath);
        }

        public void declareBuiltins()
        {
            IList<Symbol.MethodSymbol> builtins = symtab.builtins;
            for (var i = 0; i < builtins.Count; i++) {
                Symbol.MethodSymbol m = builtins[i];
                if (m.isInvoked) {
                    LLVMValueRef func = LLVM.AddFunction(module, "mj_" + m.name, typeResolver.resolve(m.type));
                    func.SetLinkage(LLVMLinkage.LLVMExternalLinkage);
                    m.llvmRef = func;
                }
            }
        }

        public void declareRuntimeHelpers()
        {
            allocFunction = module.Func(HEAP_PTR(INT8), "mjrt_alloc", PTR_VOID);
            allocFunction.SetLinkage(LLVMLinkage.LLVMExternalLinkage);

            allocArrayFunction = module.Func(HEAP_PTR(INT8), "mjrt_alloc_array", PTR_VOID, INT32);
            allocArrayFunction.SetLinkage(LLVMLinkage.LLVMExternalLinkage);

            initRuntime = module.Func(VOID, "_premain");
            initRuntime.SetLinkage(LLVMLinkage.LLVMExternalLinkage);
        }

        private void writeProlog()
        {
            LLVM.BuildCall(builder, initRuntime, new LLVMValueRef[0], "");
        }

        private void build(IList<CompilationUnit> compilationUnits)
        {
            foreach (CompilationUnit cu in compilationUnits) {
                createFunctionsAndClasses(cu);
            }
            scan(compilationUnits);
        }

        public override LLVMValueRef visitCompilationUnit(CompilationUnit compilationUnit)
        {
            scan(compilationUnit.declarations);
            return nullValue;
        }

        private void createFunctionsAndClasses(CompilationUnit compilationUnit)
        {
            // Declare classes first before processing function argumets;
            for (int i = 0, count = compilationUnit.declarations.Count; i < count; i++) {
                Tree decl = compilationUnit.declarations[i];
                if (decl is ClassDef cd) {
                    declareLLVMStructType(cd);
                }
            }

            // Struct bodies are added afterwards because of possible references
            for (int i = 0, count = compilationUnit.declarations.Count; i < count; i++) {
                Tree decl = compilationUnit.declarations[i];
                if (decl is ClassDef cd) {
                    setStructTypeBody(cd);
                    createObjectMetadataRecord(cd);
                }
            }

            foreach (var pair in symtab.arrayTypes) {
                ArrayType arrayType = pair.Value;
                declareLLVMStructType(arrayType);
                createArrayMetadataRecord(arrayType);
            }

            for (int i = 0, count = compilationUnit.declarations.Count; i < count; i++) {
                Tree decl = compilationUnit.declarations[i];
                switch (decl) {
                    case MethodDef mt:
                        createFunction(mt);
                        break;
                    case AspectDef asp:
                        createFunction(asp.after);
                        break;
                }
            }
        }

        private void declareLLVMStructType(ClassDef cd)
        {
            Symbol.ClassSymbol sym = cd.symbol;

            LLVMTypeRef structType = LLVM.StructCreateNamed(context, "struct." + cd.name);

            sym.llvmTypeRef = structType;
        }

        private void setStructTypeBody(ClassDef cd)
        {
            LLVMTypeRef[] llvmTypes = new LLVMTypeRef[cd.fields.Count + OBJECT_HEADER_FIELDS];

            // Metadata pointer, GC forwarding address, GC scan status
            llvmTypes[0] = PTR_VOID;
            llvmTypes[1] = PTR_VOID;
            llvmTypes[2] = INT8;

            // Declared fields
            for (int i = 0, count = cd.fields.Count; i < count; i++) {
                llvmTypes[i + OBJECT_HEADER_FIELDS] = typeResolver.resolve(cd.fields[i].symbol.type);
            }

            cd.symbol.llvmTypeRef.StructSetBody(llvmTypes, false);
        }

        private void declareLLVMStructType(ArrayType arrayType)
        {
            LLVMTypeRef structType = LLVM.StructCreateNamed(context, "array");
            LLVMTypeRef[] fields = {
                PTR_VOID, PTR_VOID, INT8, INT32, LLVM.ArrayType(typeResolver.resolve(arrayType.elemType), 0)
            };
            structType.StructSetBody(fields, false);

            arrayType.llvmType = structType;
        }

        private void createObjectMetadataRecord(ClassDef cd)
        {
            LLVMTypeRef objectType = cd.symbol.llvmTypeRef;
            LLVMValueRef nullPtr = NULL(PTR(objectType));

            LLVMValueRef[] ptrOffsets = cd.fields
                                          .Where(v => v.type.Tag == Tag.DECLARED_TYPE)
                                          .Select(v => {
                                              LLVMValueRef offsetGep = LLVM.ConstInBoundsGEP(nullPtr,
                                                  new[] {CONST_INT32(0), CONST_INT32(v.symbol.LLVMFieldIndex)});
                                              LLVMValueRef offset = LLVM.ConstPtrToInt(offsetGep, INT32);
                                              return offset;
                                          }).ToArray();

            LLVMValueRef offsetsArray = LLVM.ConstArray(INT32, ptrOffsets);

            LLVMValueRef metadata = LLVM.ConstStruct(new[] {
                CONST_UINT8((int)TypeKind.OBJECT), // kind
                CONST_UINT8(0), // array elem size
                SIZE_OF(objectType),
                CONST_INT32(ptrOffsets.Length), // num ref fields
                offsetsArray
            }, false);

            LLVMValueRef global = LLVM.AddGlobal(module, metadata.TypeOf(), "__meta__" + cd.name);
            global.SetInitializer(metadata);
            global.SetGlobalConstant(true);

            cd.symbol.llvmMetaRef = global;
        }

        private void createArrayMetadataRecord(ArrayType arrayType)
        {
            TypeKind kind = arrayType.elemType.IsPrimitive
                ? TypeKind.ARRAY_OF_PRIMITIVES
                : TypeKind.ARRAY_OF_REFERENCES;

            LLVMTypeRef elemTypeRef = typeResolver.resolve(arrayType.elemType);

            LLVMValueRef metadata = LLVM.ConstStruct(new[] {
                CONST_UINT8((int)kind),
                SIZE_OF(elemTypeRef, INT8), // array elem size
                SIZE_OF(arrayType.llvmType), // array size without elements (usefull for allignment)
                CONST_INT32(0) // num ref fields
            }, false);

            LLVMValueRef global = LLVM.AddGlobal(module, metadata.TypeOf(), "__meta__arr__");
            global.SetInitializer(metadata);
            global.SetGlobalConstant(true);

            arrayType.llvmMetaRef = global;
        }

        private void createFunction(MethodDef mt)
        {
            LLVMTypeRef llvmType = typeResolver.resolve(mt.symbol.type);
            LLVMValueRef func = LLVM.AddFunction(module, mt.name, llvmType);
            func.SetFunctionCallConv((uint)LLVMCallConv.LLVMCCallConv);
            func.SetGC("statepoint-example");

            mt.symbol.llvmRef = func;

            LLVMValueRef[] llvmParams = func.GetParams();
            for (var i = 0; i < llvmParams.Length; i++) {
                mt.symbol.parameters[i].llvmRef = llvmParams[i];
            }
        }

        public override LLVMValueRef visitClassDef(ClassDef classDef) => nullValue;

        public override LLVMValueRef visitMethodDef(MethodDef mt)
        {
            function = mt.symbol.llvmRef;

            LLVMBasicBlockRef entryBlock = function.AppendBasicBlock("entry");
            LLVM.PositionBuilderAtEnd(builder, entryBlock);

            if (mt.name == "main") {
                writeProlog();
            }

            variableAllocator.scan(mt.body);
            parametersToAllocas(mt.parameters);

            method = mt;
            scan(mt.body.statements);

            endFunction();
            return nullValue;
        }

        private void parametersToAllocas(IList<VariableDeclaration> parameters)
        {
            for (var i = 0; i < parameters.Count; i++) {
                VariableDeclaration param = parameters[i];
                param.symbol.llvmRef = LLVM.BuildAlloca(builder, typeResolver.resolve(param.symbol.type), param.name);
            }
            for (var i = 0; i < parameters.Count; i++) {
                VariableDeclaration param = parameters[i];
                LLVM.BuildStore(builder, function.GetParams()[i], param.symbol.llvmRef);
            }
        }

        public override LLVMValueRef visitAspectDef(AspectDef aspect)
        {
            scan(aspect.after);
            return nullValue;
        }

        private void endFunction()
        {
            LLVMBasicBlockRef lastBlock = LLVM.GetInsertBlock(builder);

            LLVMValueRef lastInst = lastBlock.GetLastInstruction();
            // if last block is empty
            if (lastInst.Pointer == IntPtr.Zero) {
                // method must be void if an empty block was produced
                // at end
                Assert.assert(method.symbol.type.ReturnType.IsVoid);

                LLVM.BuildRetVoid(builder);
                return;
            }

            LLVMOpcode prevInstOpcode = lastInst.GetInstructionOpcode();
            if (prevInstOpcode != LLVMOpcode.LLVMBr && prevInstOpcode != LLVMOpcode.LLVMRet) {
                // method must be void if it's not already terminated 
                Assert.assert(method.symbol.type.ReturnType.IsVoid);

                LLVM.BuildRetVoid(builder);
            }
        }

        public override LLVMValueRef visitBlock(Block block)
        {
            scan(block.statements);
            return nullValue;
        }

        public override LLVMValueRef visitIf(If ifStat)
        {
            // gen condition
            LLVMValueRef conditionVal = scan(ifStat.condition);

            // make "then" block
            LLVMBasicBlockRef thenBlock = function.AppendBasicBlock("then");

            LLVMBasicBlockRef afterIf = context.AppendBasicBlockInContext(function, "afterIf");

            if (ifStat.elsePart == null) {
                LLVM.BuildCondBr(builder, conditionVal, thenBlock, afterIf);
                LLVM.PositionBuilderAtEnd(builder, thenBlock);
                scan(ifStat.thenPart);
                safeJumpTo(afterIf);
                afterIf.MoveBasicBlockAfter(function.GetLastBasicBlock());
                LLVM.PositionBuilderAtEnd(builder, afterIf);
            } else {
                LLVMBasicBlockRef elseBlock = context.AppendBasicBlockInContext(function, "else");
                LLVM.BuildCondBr(builder, conditionVal, thenBlock, elseBlock);
                LLVM.PositionBuilderAtEnd(builder, thenBlock);
                scan(ifStat.thenPart);

                bool didJump = false;
                didJump |= safeJumpTo(afterIf);

                elseBlock.MoveBasicBlockAfter(function.GetLastBasicBlock());
                LLVM.PositionBuilderAtEnd(builder, elseBlock);
                scan(ifStat.elsePart);
                didJump |= safeJumpTo(afterIf);

                if (didJump) {
                    afterIf.MoveBasicBlockAfter(function.GetLastBasicBlock());
                    LLVM.PositionBuilderAtEnd(builder, afterIf);
                } else {
                    afterIf.DeleteBasicBlock();
                }
            }

            return nullValue;
        }

        public override LLVMValueRef visitWhile(WhileStatement whileStat)
        {
            LLVMBasicBlockRef whileBlock = LLVM.AppendBasicBlock(function, "while");
            LLVM.PositionBuilderAtEnd(builder, whileBlock);
            LLVMValueRef conditionVal = scan(whileStat.condition);

            LLVMBasicBlockRef thenBlock = LLVM.AppendBasicBlock(function, "then");
            LLVMBasicBlockRef afterWhile = LLVM.AppendBasicBlock(function, "afterWhile");

            whileStat.ContinueBlock = whileBlock;
            whileStat.BreakBlock = afterWhile;

            LLVM.BuildCondBr(builder, conditionVal, thenBlock, afterWhile);

            LLVM.PositionBuilderAtEnd(builder, thenBlock);
            scan(whileStat.body);
            safeJumpTo(whileBlock);

            afterWhile.MoveBasicBlockAfter(function.GetLastBasicBlock());
            LLVM.PositionBuilderAtEnd(builder, afterWhile);

            return nullValue;
        }

        public override LLVMValueRef visitDo(DoStatement doStat)
        {
            LLVMBasicBlockRef doBlock = LLVM.AppendBasicBlock(function, "do");
            LLVMBasicBlockRef afterDo = LLVM.AppendBasicBlock(function, "afterDo");

            doStat.ContinueBlock = doBlock;
            doStat.BreakBlock = afterDo;

            // explicit jump from the previous block as required by LLVM
            safeJumpTo(doBlock);

            LLVM.PositionBuilderAtEnd(builder, doBlock);
            scan(doStat.body);
            LLVMValueRef conditionVal = scan(doStat.condition);
            LLVM.BuildCondBr(builder, conditionVal, doBlock, afterDo);

            afterDo.MoveBasicBlockAfter(function.GetLastBasicBlock());
            LLVM.PositionBuilderAtEnd(builder, afterDo);

            return nullValue;
        }

        public override LLVMValueRef visitFor(ForLoop forLoop)
        {
            scan(forLoop.init);
            LLVMBasicBlockRef forBlock = LLVM.AppendBasicBlock(function, "for");
            safeJumpTo(forBlock);

            LLVMBasicBlockRef afterFor = LLVM.AppendBasicBlock(function, "afterfor");

            forLoop.ContinueBlock = forBlock;
            forLoop.BreakBlock = afterFor;

            LLVM.PositionBuilderAtEnd(builder, forBlock);
            if (forLoop.condition != null) {
                LLVMValueRef condValue = scan(forLoop.condition);
                LLVMBasicBlockRef thenBlock = function.AppendBasicBlock("then");
                LLVM.BuildCondBr(builder, condValue, thenBlock, afterFor);
                LLVM.PositionBuilderAtEnd(builder, thenBlock);
            }

            scan(forLoop.body);

            scan(forLoop.update);
            safeJumpTo(forBlock);

            afterFor.MoveBasicBlockAfter(function.GetLastBasicBlock());
            LLVM.PositionBuilderAtEnd(builder, afterFor);

            return nullValue;
        }

        public override LLVMValueRef visitSwitch(Switch @switch)
        {
            LLVMValueRef selectorVal = scan(@switch.selector);

            if (@switch.cases.Count == 0) {
                return nullValue;
            }

            int numRealCases = @switch.cases.Count(@case => @case.expression != null);

            LLVMBasicBlockRef dummy = LLVM.AppendBasicBlock(function, "dummy");
            LLVMValueRef switchInst = LLVM.BuildSwitch(builder, selectorVal, dummy, (uint)numRealCases);

            LLVMBasicBlockRef? defaultBlock = null;

            LLVMBasicBlockRef afterSwitch = LLVM.AppendBasicBlock(function, "afterSwitch");
            @switch.BreakBlock = afterSwitch;

            for (var i = 0; i < @switch.cases.Count; i++) {
                Case @case = @switch.cases[i];
                LLVMBasicBlockRef caseBlock = function.AppendBasicBlock("case");

                if (i > 0) {
                    safeJumpTo(caseBlock);
                }

                if (@case.expression != null) {
                    LLVMValueRef caseVal = scan(@case.expression);
                    Assert.assert(caseVal.IsConstant());
                    switchInst.AddCase(caseVal, caseBlock);
                } else {
                    defaultBlock = caseBlock;
                }

                LLVM.PositionBuilderAtEnd(builder, caseBlock);
                scan(@case.statements);
            }

            switchInst.SetOperand(1, defaultBlock ?? afterSwitch);
            dummy.DeleteBasicBlock();

            if (@switch.didBreak || safeJumpTo(afterSwitch)) {
                afterSwitch.MoveBasicBlockAfter(function.GetLastBasicBlock());
                LLVM.PositionBuilderAtEnd(builder, afterSwitch);
            } else {
                afterSwitch.DeleteBasicBlock();
            }

            return nullValue;
        }

        // result is used by visitIf and visitSwitch to determine
        // if it's needed or not to emit the "after" block
        // if we dont jump here, that means all branches have
        // terminating statements, which means it is impossible to
        // continue 
        private bool safeJumpTo(LLVMBasicBlockRef destBlock)
        {
            LLVMBasicBlockRef prevBlock = LLVM.GetInsertBlock(builder);
            LLVMValueRef lastInst = prevBlock.GetLastInstruction();
            if (lastInst.Pointer == IntPtr.Zero) {
                LLVM.BuildBr(builder, destBlock);
                return true;
            }

            LLVMOpcode prevInstOpcode = lastInst.GetInstructionOpcode();
            if (prevInstOpcode != LLVMOpcode.LLVMBr && prevInstOpcode != LLVMOpcode.LLVMRet) {
                LLVM.BuildBr(builder, destBlock);
                return true;
            }

            return false;
        }

        public override LLVMValueRef visitBreak(Break @break)
        {
            @break.target.setBreak();
            LLVM.BuildBr(builder, @break.target.BreakBlock);
            return nullValue;
        }

        public override LLVMValueRef visitContinue(Continue @continue)
        {
            LLVM.BuildBr(builder, @continue.target.ContinueBlock);
            return nullValue;
        }

        public override LLVMValueRef visitReturn(ReturnStatement returnStatement)
        {
            if (returnStatement.value == null) {
                LLVM.BuildRetVoid(builder);
            } else {
                LLVMValueRef value = scan(returnStatement.value);
                value = promote(returnStatement.value.type, method.symbol.type.ReturnType, value);
                if (returnStatement.afterAspects != null) {
                    scan(returnStatement.afterAspects);
                }
                LLVM.BuildRet(builder, value);
            }

            return nullValue;
        }

        public override LLVMValueRef visitExpresionStmt(ExpressionStatement expr)
        {
            scan(expr.expression);
            return nullValue;
        }

        public override LLVMValueRef visitVarDef(VariableDeclaration varDef)
        {
            if (varDef.init != null) {
                LLVMValueRef initVal = scan(varDef.init);
                initVal = promote(varDef.init.type, varDef.symbol.type, initVal);
                LLVM.BuildStore(builder, initVal, varDef.symbol.llvmRef);
            }

            return nullValue;
        }

        public override LLVMValueRef visitConditional(ConditionalExpression conditional)
        {
            LLVMValueRef conditionVal = scan(conditional.condition);

            LLVMBasicBlockRef thenBlock = LLVM.AppendBasicBlock(function, "then");
            LLVMBasicBlockRef elseBlock = LLVM.AppendBasicBlock(function, "else");
            LLVMBasicBlockRef afterIf = LLVM.AppendBasicBlock(function, "afterIf");

            LLVM.BuildCondBr(builder, conditionVal, thenBlock, elseBlock);

            LLVM.PositionBuilderAtEnd(builder, thenBlock);
            Expression trueExpr = conditional.ifTrue;
            LLVMValueRef trueVal = scan(trueExpr);
            trueVal = promote(trueExpr.type, conditional.type, trueVal);
            safeJumpTo(afterIf);
            thenBlock = LLVM.GetInsertBlock(builder);

            elseBlock.MoveBasicBlockAfter(function.GetLastBasicBlock());
            LLVM.PositionBuilderAtEnd(builder, elseBlock);
            Expression falseExpr = conditional.ifFalse;
            LLVMValueRef falseVal = scan(falseExpr);
            falseVal = promote(falseExpr.type, conditional.type, falseVal);
            safeJumpTo(afterIf);
            elseBlock = LLVM.GetInsertBlock(builder);

            afterIf.MoveBasicBlockAfter(function.GetLastBasicBlock());
            LLVM.PositionBuilderAtEnd(builder, afterIf);

            LLVMValueRef phi = LLVM.BuildPhi(builder, conditional.type.accept(typeResolver), "cond");
            phi.AddIncoming(new[] {trueVal, falseVal}, new[] {thenBlock, elseBlock}, 2);
            return phi;
        }

        public override LLVMValueRef visitMethodInvoke(MethodInvocation mi)
        {
            int fixedCount = mi.methodSym.type.ParameterTypes.Count;
            int callCount = mi.args.Count;

            LLVMValueRef[] args = new LLVMValueRef[callCount];
            int i = 0;
            for (; i < fixedCount; i++) {
                Expression arg = mi.args[i];
                LLVMValueRef argVal = scan(arg);
                args[i] = promote(arg.type, mi.methodSym.type.ParameterTypes[i], argVal);
            }

            // add varargs without promotion
            for (; i < callCount; i++) {
                Expression arg = mi.args[i];
                args[i] = scan(arg);
            }

            string name = mi.type.IsVoid ? "" : mi.methodSym.name;
            LLVMValueRef callInst = LLVM.BuildCall(builder, mi.methodSym.llvmRef, args, name);
            return callInst;
        }

        public override LLVMValueRef visitIdent(Identifier ident)
        {
            return LLVM.BuildLoad(builder, ident.symbol.llvmRef, "load." + ident.name);
        }

        public override LLVMValueRef visitSelect(Select select)
        {
            LLVMValueRef left = scan(select.selectBase);
            LLVMValueRef ptr =
                LLVM.BuildStructGEP(builder, left, (uint)select.symbol.LLVMFieldIndex, select.name + ".ptr");
            return LLVM.BuildLoad(builder, ptr, select.name);
        }

        public override LLVMValueRef visitIndex(ArrayIndex indexExpr)
        {
            LLVMValueRef array = scan(indexExpr.indexBase);
            LLVMValueRef index = scan(indexExpr.index);
            // GEP indexing: Obligatory 0 > elems field number is 4 > index into array
            LLVMValueRef ptr =
                LLVM.BuildInBoundsGEP(builder, array, new[] {CONST_INT32(0), CONST_INT32(4), index}, "elem.ptr");
            return LLVM.BuildLoad(builder, ptr, "elem");
        }

        public override LLVMValueRef visitNewClass(NewClass newClass)
        {
            Symbol.ClassSymbol sym = newClass.symbol;

            LLVMValueRef metaPtr = LLVM.BuildPointerCast(builder, sym.llvmMetaRef, PTR_VOID, "met");
            LLVMValueRef untypedPtr = LLVM.BuildCall(builder, allocFunction, new[] {metaPtr}, "tmp");
            
            return LLVM.BuildPointerCast(builder, untypedPtr, HEAP_PTR(sym.llvmTypeRef), "tmp");
        }

        public override LLVMValueRef visitNewArray(NewArray newArray)
        {
            ArrayType arrayType = (ArrayType)newArray.type;
            Expression lengthExpr = newArray.length;
            LLVMValueRef length = scan(lengthExpr);
            length = promote(lengthExpr.type, symtab.intType, length);

            LLVMValueRef metaPtr = LLVM.BuildPointerCast(builder, arrayType.llvmMetaRef, PTR_VOID, "met");

            LLVMValueRef untypedPtr = LLVM.BuildCall(builder, allocArrayFunction, new[] {metaPtr, length}, "tmp");
            return LLVM.BuildPointerCast(builder, untypedPtr, HEAP_PTR(arrayType.llvmType), "tmp");
        }

        public override LLVMValueRef visitLiteral(LiteralExpression literal)
        {
            switch (literal.typeTag) {
                case TypeTag.INT:
                    return LLVM.ConstInt(INT32, (ulong)(int)literal.value, true);
                case TypeTag.LONG:
                    return LLVM.ConstInt(INT64, (ulong)(long)literal.value, true);
                case TypeTag.FLOAT:
                    return LLVM.ConstReal(FLOAT, (float)literal.value);
                case TypeTag.DOUBLE:
                    return LLVM.ConstReal(DOUBLE, (double)literal.value);
                case TypeTag.BOOLEAN:
                    return LLVM.ConstInt(INT1, (ulong)((bool)literal.value ? 1 : 0), false);
                case TypeTag.STRING:
                    return LLVM.BuildGlobalStringPtr(builder, (string)literal.value, "strLit");
                case TypeTag.NULL:
                    return dummyNullPtr;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override LLVMValueRef visitAssign(AssignNode expr)
        {
            LLVMValueRef assignValue = scan(expr.right);

            doAssign(getPointerToLocation(expr.left), assignValue);

            return assignValue;
        }

        public override LLVMValueRef visitCompoundAssign(CompoundAssignNode expr)
        {
            LLVMValueRef resValue = doBinary(expr.left, expr.right, expr.operatorSym);

            doAssign(getPointerToLocation(expr.left), resValue);

            return resValue;
        }

        private void doAssign(LLVMValueRef llvmPointer, LLVMValueRef value)
        {
            LLVM.BuildStore(builder, value, llvmPointer);
        }

        private LLVMValueRef getPointerToLocation(Expression expr)
        {
            switch (expr) {
                case Identifier id: return id.symbol.llvmRef;
                case Select s:
                    LLVMValueRef ptrToPtrToObject = getPointerToLocation(s.selectBase);
                    LLVMValueRef ptrToObject = LLVM.BuildLoad(builder, ptrToPtrToObject, "tmp");
                    LLVMValueRef ptrToField = LLVM.BuildStructGEP(builder, ptrToObject,
                        (uint)s.symbol.LLVMFieldIndex, s.name + ".ptr");
                    return ptrToField;
                case ArrayIndex ind:
                    LLVMValueRef index = scan(ind.index);
                    LLVMValueRef ptrToPtrToArray = getPointerToLocation(ind.indexBase);
                    LLVMValueRef ptrToArray = LLVM.BuildLoad(builder, ptrToPtrToArray, "tmp");
                    LLVMValueRef ptrToElem = LLVM.BuildInBoundsGEP(builder, ptrToArray, 
                        new[] {CONST_INT32(0), CONST_INT32(4), index}, "elem.ptr");
                    return ptrToElem;
            }
            throw new InvalidOperationException();
        }

        public override LLVMValueRef visitUnary(UnaryExpressionNode expr)
        {
            LLVMValueRef operandVal = scan(expr.operand);
            switch (expr.opcode) {
                case Tag.NEG:
                    if (expr.operand.type.IsIntegral) {
                        return LLVM.BuildNeg(builder, operandVal, "neg");
                    } else {
                        return LLVM.BuildFNeg(builder, operandVal, "fneg");
                    }
                case Tag.NOT:
                case Tag.COMPL:
                    return LLVM.BuildNot(builder, operandVal, "not");
            }

            if (expr.opcode.isIncDec()) {
                LLVMValueRef one = toLLVMConst(1, expr.operatorSym.type.ReturnType.Tag);
                LLVMValueRef resVal = LLVM.BuildBinOp(builder, expr.operatorSym.opcode, operandVal, one, "incDec");
                LLVM.BuildStore(builder, resVal, ((Identifier)expr.operand).symbol.llvmRef);
                return expr.opcode.isPre() ? resVal : operandVal;
            }

            throw new ArgumentOutOfRangeException();
        }

        private LLVMValueRef toLLVMConst(int num, TypeTag type)
        {
            switch (type) {
                case TypeTag.INT: return CONST_INT32(num);
                case TypeTag.LONG: return CONST_INT64(num);
                case TypeTag.FLOAT: return CONST_FLOAT(num);
                case TypeTag.DOUBLE: return CONST_DOUBLE(num);
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public override LLVMValueRef visitBinary(BinaryExpressionNode expr)
        {
            if (expr.opcode == Tag.AND || expr.opcode == Tag.OR) {
                return buildShortCircutLogical(expr.opcode == Tag.OR, expr.left, expr.right);
            }
            return doBinary(expr.left, expr.right, expr.operatorSym);
        }

        private LLVMValueRef doBinary(Expression left, Expression right, Symbol.OperatorSymbol op)
        {
            LLVMValueRef leftVal = scan(left);
            LLVMValueRef rightVal = scan(right);

            if (op.type.ReturnType.IsNumeric || op.IsComparison) {
                leftVal = promote((PrimitiveType)left.type, (PrimitiveType)op.type.ParameterTypes[0], leftVal);
                rightVal = promote((PrimitiveType)right.type, (PrimitiveType)op.type.ParameterTypes[1], rightVal);
            }

            return makeBinary(op, leftVal, rightVal);
        }

        public LLVMValueRef makeBinary(Symbol.OperatorSymbol binary, LLVMValueRef leftVal, LLVMValueRef rightVal)
        {
            LLVMOpcode llvmOpcode = binary.opcode;
            if (llvmOpcode == LLVMOpcode.LLVMICmp) {
                return LLVM.BuildICmp(builder, (LLVMIntPredicate)binary.llvmPredicate, leftVal, rightVal, "icmp");
            }

            if (llvmOpcode == LLVMOpcode.LLVMFCmp) {
                return LLVM.BuildFCmp(builder, (LLVMRealPredicate)binary.llvmPredicate, leftVal, rightVal, "fcmp");
            }

            return LLVM.BuildBinOp(builder, llvmOpcode, leftVal, rightVal, "tmp");
        }

        public LLVMValueRef buildShortCircutLogical(bool isOR, Expression left, Expression right)
        {
            LLVMValueRef leftVal = scan(left);
            LLVMBasicBlockRef prevBlock = LLVM.GetInsertBlock(builder);

            LLVMBasicBlockRef rightBlock = LLVM.AppendBasicBlock(function, "right");
            LLVMBasicBlockRef afterIf = LLVM.AppendBasicBlock(function, "afterSC");

            if (isOR) {
                LLVM.BuildCondBr(builder, leftVal, afterIf, rightBlock);
            } else {
                LLVM.BuildCondBr(builder, leftVal, rightBlock, afterIf);
            }

            rightBlock.MoveBasicBlockAfter(function.GetLastBasicBlock());
            LLVM.PositionBuilderAtEnd(builder, rightBlock);
            LLVMValueRef rightVal = scan(right);
            safeJumpTo(afterIf);
            rightBlock = LLVM.GetInsertBlock(builder);

            afterIf.MoveBasicBlockAfter(function.GetLastBasicBlock());
            LLVM.PositionBuilderAtEnd(builder, afterIf);

            LLVMValueRef phi = LLVM.BuildPhi(builder, INT1, "res");

            LLVMValueRef phiLeftVal = LLVM.ConstInt(INT1, (ulong)(isOR ? 1 : 0), false);

            phi.AddIncoming(new[] {phiLeftVal, rightVal}, new[] {prevBlock, rightBlock}, 2);
            return phi;
        }

        public LLVMValueRef promote(Type actualType, Type requestedType, LLVMValueRef value)
        {
            // Compare base types to acount for constants
            if (actualType.BaseType == requestedType.BaseType) {
                return value;
            }

            Assert.assert(typings.isAssignableFrom(requestedType, actualType));

            if (actualType == symtab.bottomType) {
                LLVMTypeRef reqTypeRef = typeResolver.resolve(requestedType);
                return LLVM.ConstPointerNull(reqTypeRef);
            }
            
            switch (requestedType.Tag) {
                case TypeTag.LONG:
                    return LLVM.BuildSExt(builder, value, LLVMTypeRef.Int64Type(), "toLong");
                case TypeTag.FLOAT:
                    switch (actualType.Tag) {
                        case TypeTag.INT:
                        case TypeTag.LONG:
                            return LLVM.BuildSIToFP(builder, value, LLVMTypeRef.FloatType(), "toFloat");
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                case TypeTag.DOUBLE:
                    switch (actualType.Tag) {
                        case TypeTag.INT:
                        case TypeTag.LONG:
                            return LLVM.BuildSIToFP(builder, value, LLVMTypeRef.DoubleType(), "toDouble");
                        case TypeTag.FLOAT:
                            return LLVM.BuildFPExt(builder, value, LLVMTypeRef.DoubleType(), "toDouble");
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private TargetOS getTargetOS()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                return TargetOS.Windows;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                return TargetOS.OSX;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                return TargetOS.Linux;
            }
            throw new ArgumentOutOfRangeException("OSPlatform unknown");
        }

        private enum TargetOS
        {
            Windows,
            OSX,
            Linux
        }
    }
}
