﻿using System;
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
    public class CodeGeneration : AstVisitor<LLVMValueRef>
    {
        private static readonly Context.Key<CodeGeneration> CONTEXT_KEY = new Context.Key<CodeGeneration>();

        public static CodeGeneration instance(Context ctx) =>
            ctx.tryGet(CONTEXT_KEY, out var instance) ? instance : new CodeGeneration(ctx);

        private readonly LLVMTypeResolver typeResolver;
        private VariableAllocator variableAllocator;
        private readonly Typings typings;
        private readonly CommandLineOptions options;
        private readonly Symtab symtab;
        private readonly Log log;

        private readonly TargetOS targetOS;

        public CodeGeneration(Context ctx)
        {
            ctx.put(CONTEXT_KEY, this);

            typings = Typings.instance(ctx);
            typeResolver = new LLVMTypeResolver();
            options = CommandLineOptions.instance(ctx);
            symtab = Symtab.instance(ctx);
            log = Log.instance(ctx);

            targetOS = getTargetOS();
        }

        private LLVMContextRef context;
        private LLVMModuleRef module;
        private LLVMValueRef function;
        private FuncDef funcDef;
        private LLVMBuilderRef builder;

        private LLVMValueRef initRuntime;
        private LLVMValueRef allocFunction;
        private LLVMValueRef allocArrayFunction;

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

            foreach ((Type type, ArrayType arrayType) in symtab.arrayTypes) {
                if (type.IsPrimitive) {
                    declareLLVMStructType(arrayType);
                    createArrayMetadataRecord(arrayType);
                }
            }
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
            string str       = Marshal.PtrToStringUTF8(stringPtr);
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

#if EMIT_TO_FILE
            string message;
            unsafe {
                fixed (char* fileName = options.OutPath) {
                    LLVM.TargetMachineEmitToFile(targetMachine, module, new IntPtr(fileName),
                        LLVMCodeGenFileType.LLVMObjectFile, out message);
                    LLVM.DisposeTargetMachine(targetMachine);
                }
            }
            if (!String.IsNullOrEmpty(message)) {
                log.globalError("Failed to emit object file. LLVM Error: {0}", message);
            }
#else       
            LLVM.TargetMachineEmitToMemoryBuffer(targetMachine, module, LLVMCodeGenFileType.LLVMObjectFile,
                out var _, out var memoryBuffer);

            size_t bufferSize  = LLVM.GetBufferSize(memoryBuffer);
            IntPtr bufferStart = LLVM.GetBufferStart(memoryBuffer);

            unsafe {
                Stream   s    = new UnmanagedMemoryStream((byte*)bufferStart.ToPointer(), bufferSize);
                FileInfo file = new FileInfo(options.OutPath);
                if (file.Exists) {
                    file.Delete();
                }

                using (FileStream fs = file.Open(FileMode.Create, FileAccess.Write))
                using (DisposableBuffer unused = new DisposableBuffer(memoryBuffer)) {
                    s.CopyTo(fs);
                }
            }
#endif
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
            IList<Symbol.FuncSymbol> builtins = symtab.builtins;
            for (int i = 0; i < builtins.Count; i++) {
                Symbol.FuncSymbol f = builtins[i];
                if (f.isInvoked) {
                    LLVMValueRef func = LLVM.AddFunction(module, "mj_" + f.name, typeResolver.resolve(f.type));
                    func.SetLinkage(LLVMLinkage.LLVMExternalLinkage);
                    f.llvmRef = func;
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
                createFunctionsAndClasses(cu.declarations);
            }

            scan(compilationUnits);
        }

        public override LLVMValueRef visitCompilationUnit(CompilationUnit compilationUnit)
        {
            scan(compilationUnit.declarations);
            return default;
        }

        private void createFunctionsAndClasses(IList<Tree> declarations)
        {
            // Declare classes first before processing function argumets;
            for (int i = 0, count = declarations.Count; i < count; i++) {
                Tree decl = declarations[i];
                if (decl is StructDef st) {
                    declareLLVMStructType(st);
                }
            }

            // Struct bodies are added afterwards because of possible references
            for (int i = 0, count = declarations.Count; i < count; i++) {
                Tree decl = declarations[i];
                if (decl is StructDef st) {
                    setStructTypeBody(st);
                    createObjectMetadataRecord(st);
                }
            }

            foreach (var (type, arrayType) in symtab.arrayTypes) {
                if (!type.IsPrimitive) {
                    declareLLVMStructType(arrayType);
                    createArrayMetadataRecord(arrayType);
                }
            }

            for (int i = 0, count = declarations.Count; i < count; i++) {
                Tree decl = declarations[i];
                switch (decl.Tag) {
                    case Tag.FUNC_DEF:
                        createFreeFunction((FuncDef)decl);
                        break;
                    case Tag.STRUCT_DEF:
                        StructDef sdef = (StructDef)decl;
                        for (int j = 0; j < sdef.members.Count; j++) {
                            Tree member = sdef.members[j];
                            if (member.Tag == Tag.FUNC_DEF) {
                                createMemberFunction((FuncDef)member);
                            }
                        }
                        break;
                }
            }
        }

        private void declareLLVMStructType(StructDef st)
        {
            Symbol.StructSymbol sym = st.symbol;

            LLVMTypeRef structType = LLVM.StructCreateNamed(context, "struct." + st.name);

            sym.llvmTypeRef = structType;
        }

        private void setStructTypeBody(StructDef st)
        {
            LLVMTypeRef[] llvmTypes =
                new LLVMTypeRef[st.members.Count(m => m.Tag == Tag.VAR_DEF) + OBJECT_HEADER_FIELDS];

            // Metadata pointer, GC forwarding address, GC scan status
            llvmTypes[0] = PTR_VOID;
            llvmTypes[1] = PTR_VOID;
            llvmTypes[2] = INT8;

            // Declared fields
            int fieldIndex = 0;
            foreach (Tree member in st.members) {
                if (member.Tag == Tag.VAR_DEF) {
                    VariableDeclaration field = (VariableDeclaration)member;
                    field.symbol.fieldIndex = fieldIndex;
                    llvmTypes[fieldIndex + OBJECT_HEADER_FIELDS] = typeResolver.resolve(field.symbol.type);
                    fieldIndex++;
                }
            }

            st.symbol.llvmTypeRef.StructSetBody(llvmTypes, false);
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

        private void createObjectMetadataRecord(StructDef st)
        {
            LLVMTypeRef  objectType = st.symbol.llvmTypeRef;
            LLVMValueRef nullPtr    = NULL(PTR(objectType));

            List<LLVMValueRef> list = new List<LLVMValueRef>();
            foreach (Tree m in st.members) {
                if (m.Tag == Tag.VAR_DEF) {
                    VariableDeclaration f = (VariableDeclaration)m;
                    if (f.type.Tag == Tag.DECLARED_TYPE) {
                        LLVMValueRef offsetGep = LLVM.ConstInBoundsGEP(nullPtr,
                            new[] {CONST_INT32(0), CONST_INT32(f.symbol.LLVMFieldIndex)});
                        list.Add(LLVM.ConstPtrToInt(offsetGep, INT32));
                    }
                }
            }
            LLVMValueRef[] ptrOffsets = list.ToArray();

            LLVMValueRef offsetsArray = LLVM.ConstArray(INT32, ptrOffsets);

            LLVMValueRef metadata = LLVM.ConstStruct(new[] {
                CONST_UINT8((int)TypeKind.OBJECT), // kind
                CONST_UINT8(0), // array elem size
                SIZE_OF(objectType),
                CONST_INT32(ptrOffsets.Length), // num ref fields
                offsetsArray
            }, false);

            LLVMValueRef global = LLVM.AddGlobal(module, metadata.TypeOf(), "__meta__" + st.name);
            global.SetInitializer(metadata);
            global.SetGlobalConstant(true);

            st.symbol.llvmMetaRef = global;
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

        private void createFreeFunction(FuncDef funcDef)
        {
            LLVMTypeRef llvmType = typeResolver.resolve(funcDef.symbol.type);
            string      name     = funcDef.name;
            if (name != "main") {
                name = "F_" + name;
            }

            LLVMValueRef func = LLVM.AddFunction(module, name, llvmType);
            func.SetFunctionCallConv((uint)LLVMCallConv.LLVMCCallConv);
            func.SetGC("statepoint-example");

            funcDef.symbol.llvmRef = func;

            LLVMValueRef[] llvmParams = func.GetParams();
            for (var i = 0; i < llvmParams.Length; i++) {
                funcDef.symbol.parameters[i].llvmRef = llvmParams[i];
            }
        }

        private void createMemberFunction(FuncDef funcDef)
        {
            LLVMTypeRef llvmType = typeResolver.resolve(funcDef.symbol.type);

            string name = "M_" + funcDef.symbol.owner.name + "_" + funcDef.name;

            LLVMValueRef func = LLVM.AddFunction(module, name, llvmType);
            func.SetFunctionCallConv((uint)LLVMCallConv.LLVMCCallConv);
            func.SetGC("statepoint-example");

            funcDef.symbol.llvmRef = func;

            // includes "this" parameter
            LLVMValueRef[] llvmParams = func.GetParams();

            // skip "this" parameter
            for (int i = 1; i < llvmParams.Length; i++) {
                funcDef.symbol.parameters[i - 1].llvmRef = llvmParams[i];
            }
        }

        public override LLVMValueRef visitStructDef(StructDef structDef)
        {
            foreach (Tree member in structDef.members) {
                if (member.Tag == Tag.FUNC_DEF) {
                    visitFuncDef((FuncDef)member);
                }
            }
            return default;
        }

        public override LLVMValueRef visitFuncDef(FuncDef func)
        {
            function = func.symbol.llvmRef;

            LLVMBasicBlockRef entryBlock = function.AppendBasicBlock("entry");
            LLVM.PositionBuilderAtEnd(builder, entryBlock);

            if (func.name == "main") {
                writeProlog();
            }

            variableAllocator.scan(func.body);
            parametersToAllocas(func.parameters, func.symbol.owner.kind == Symbol.Kind.STRUCT);

            funcDef = func;
            scan(func.body.statements);

            endFunction();
            return default;
        }

        private void parametersToAllocas(IList<VariableDeclaration> parameters, bool isMember)
        {
            for (int i = 0; i < parameters.Count; i++) {
                VariableDeclaration param = parameters[i];
                param.symbol.llvmRef = LLVM.BuildAlloca(builder, typeResolver.resolve(param.symbol.type), param.name);
            }

            int shift = isMember ? 1 : 0;
            for (int i = 0; i < parameters.Count; i++) {
                VariableDeclaration param = parameters[i];
                LLVM.BuildStore(builder, function.GetParam((uint)(i + shift)), param.symbol.llvmRef);
            }
        }

        private void endFunction()
        {
            LLVMBasicBlockRef lastBlock = LLVM.GetInsertBlock(builder);

            LLVMValueRef lastInst = lastBlock.GetLastInstruction();
            // if last block is empty
            if (lastInst.Pointer == IntPtr.Zero) {
                // func must be void if an empty block was produced
                // at end
                Assert.assert(funcDef.symbol.type.ReturnType.IsVoid);

                LLVM.BuildRetVoid(builder);
                return;
            }

            LLVMOpcode prevInstOpcode = lastInst.GetInstructionOpcode();
            if (prevInstOpcode != LLVMOpcode.LLVMBr && prevInstOpcode != LLVMOpcode.LLVMRet) {
                // func must be void if it's not already terminated 
                Assert.assert(funcDef.symbol.type.ReturnType.IsVoid);

                LLVM.BuildRetVoid(builder);
            }
        }

        public override LLVMValueRef visitBlock(Block block)
        {
            scan(block.statements);
            return default;
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

            return default;
        }

        public override LLVMValueRef visitWhile(WhileStatement whileStat)
        {
            LLVMBasicBlockRef whileBlock = LLVM.AppendBasicBlock(function, "while");
            safeJumpTo(whileBlock);
            LLVM.PositionBuilderAtEnd(builder, whileBlock);
            LLVMValueRef conditionVal = scan(whileStat.condition);

            LLVMBasicBlockRef thenBlock  = LLVM.AppendBasicBlock(function, "then");
            LLVMBasicBlockRef afterWhile = LLVM.AppendBasicBlock(function, "afterWhile");

            whileStat.ContinueBlock = whileBlock;
            whileStat.BreakBlock = afterWhile;

            LLVM.BuildCondBr(builder, conditionVal, thenBlock, afterWhile);

            LLVM.PositionBuilderAtEnd(builder, thenBlock);
            scan(whileStat.body);
            safeJumpTo(whileBlock);

            afterWhile.MoveBasicBlockAfter(function.GetLastBasicBlock());
            LLVM.PositionBuilderAtEnd(builder, afterWhile);

            return default;
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

            return default;
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
                LLVMValueRef      condValue = scan(forLoop.condition);
                LLVMBasicBlockRef thenBlock = function.AppendBasicBlock("then");
                LLVM.BuildCondBr(builder, condValue, thenBlock, afterFor);
                LLVM.PositionBuilderAtEnd(builder, thenBlock);
            }

            scan(forLoop.body);

            scan(forLoop.update);
            safeJumpTo(forBlock);

            afterFor.MoveBasicBlockAfter(function.GetLastBasicBlock());
            LLVM.PositionBuilderAtEnd(builder, afterFor);

            return default;
        }

        public override LLVMValueRef visitSwitch(Switch @switch)
        {
            LLVMValueRef selectorVal = scan(@switch.selector);

            if (@switch.cases.Count == 0) {
                return default;
            }

            int numRealCases = @switch.cases.Count(@case => @case.expression != null);

            LLVMBasicBlockRef dummy      = LLVM.AppendBasicBlock(function, "dummy");
            LLVMValueRef      switchInst = LLVM.BuildSwitch(builder, selectorVal, dummy, (uint)numRealCases);

            LLVMBasicBlockRef? defaultBlock = null;

            LLVMBasicBlockRef afterSwitch = LLVM.AppendBasicBlock(function, "afterSwitch");
            @switch.BreakBlock = afterSwitch;

            for (var i = 0; i < @switch.cases.Count; i++) {
                Case              @case     = @switch.cases[i];
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

            return default;
        }

        // result is used by visitIf and visitSwitch to determine
        // if it's needed or not to emit the "after" block
        // if we dont jump here, that means all branches have
        // terminating statements, which means it is impossible to
        // continue 
        private bool safeJumpTo(LLVMBasicBlockRef destBlock)
        {
            LLVMBasicBlockRef prevBlock = LLVM.GetInsertBlock(builder);
            LLVMValueRef      lastInst  = prevBlock.GetLastInstruction();
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
            return default;
        }

        public override LLVMValueRef visitContinue(Continue @continue)
        {
            LLVM.BuildBr(builder, @continue.target.ContinueBlock);
            return default;
        }

        public override LLVMValueRef visitReturn(ReturnStatement returnStatement)
        {
            if (returnStatement.value == null) {
                LLVM.BuildRetVoid(builder);
            } else {
                LLVMValueRef value = scan(returnStatement.value);
                value = promote(returnStatement.value.type, funcDef.symbol.type.ReturnType, value);

                LLVM.BuildRet(builder, value);
            }

            return default;
        }

        public override LLVMValueRef visitExpresionStmt(ExpressionStatement expr)
        {
            scan(expr.expression);
            return default;
        }

        public override LLVMValueRef visitVarDef(VariableDeclaration varDef)
        {
            if (varDef.init != null) {
                LLVMValueRef initVal = scan(varDef.init);
                initVal = promote(varDef.init.type, varDef.symbol.type, initVal);
                LLVM.BuildStore(builder, initVal, varDef.symbol.llvmRef);
            }

            return default;
        }

        public override LLVMValueRef visitConditional(ConditionalExpression conditional)
        {
            LLVMValueRef conditionVal = scan(conditional.condition);

            LLVMBasicBlockRef thenBlock = LLVM.AppendBasicBlock(function, "then");
            LLVMBasicBlockRef elseBlock = LLVM.AppendBasicBlock(function, "else");
            LLVMBasicBlockRef afterIf   = LLVM.AppendBasicBlock(function, "afterIf");

            LLVM.BuildCondBr(builder, conditionVal, thenBlock, elseBlock);

            LLVM.PositionBuilderAtEnd(builder, thenBlock);
            Expression   trueExpr = conditional.ifTrue;
            LLVMValueRef trueVal  = scan(trueExpr);
            trueVal = promote(trueExpr.type, conditional.type, trueVal);
            safeJumpTo(afterIf);
            thenBlock = LLVM.GetInsertBlock(builder);

            elseBlock.MoveBasicBlockAfter(function.GetLastBasicBlock());
            LLVM.PositionBuilderAtEnd(builder, elseBlock);
            Expression   falseExpr = conditional.ifFalse;
            LLVMValueRef falseVal  = scan(falseExpr);
            falseVal = promote(falseExpr.type, conditional.type, falseVal);
            safeJumpTo(afterIf);
            elseBlock = LLVM.GetInsertBlock(builder);

            afterIf.MoveBasicBlockAfter(function.GetLastBasicBlock());
            LLVM.PositionBuilderAtEnd(builder, afterIf);

            LLVMValueRef phi = LLVM.BuildPhi(builder, conditional.type.accept(typeResolver), "cond");
            phi.AddIncoming(new[] {trueVal, falseVal}, new[] {thenBlock, elseBlock}, 2);
            return phi;
        }

        public override LLVMValueRef visitFuncInvoke(FuncInvocation fi)
        {
            int fixedCount = fi.funcSym.type.ParameterTypes.Count;
            int callCount  = fi.args.Count;

            bool isMember = fi.funcSym.owner.kind == Symbol.Kind.STRUCT;
            LLVMValueRef[] args = new LLVMValueRef[isMember ? callCount + 1 : callCount];

            int add;
            if (isMember) {
                args[0] = function.GetParam(0);
                add = 1;
            } else {
                add = 0;
            }
            int i = 0;
            for (; i < fixedCount; i++) {
                Expression   arg    = fi.args[i];
                LLVMValueRef argVal = scan(arg);
                args[i + add] = promote(arg.type, fi.funcSym.type.ParameterTypes[i], argVal);
            }

            // add varargs without promotion
            for (; i < callCount; i++) {
                Expression arg = fi.args[i];
                args[i + add] = scan(arg);
            }

            string       name     = fi.type.IsVoid ? "" : fi.funcSym.name;
            LLVMValueRef callInst = LLVM.BuildCall(builder, fi.funcSym.llvmRef, args, name);
            return callInst;
        }

        public override LLVMValueRef visitMethodInvoke(MethodInvocation mi)
        {
            int  fixedCount = mi.funcSym.type.ParameterTypes.Count;
            int  callCount  = mi.args.Count;

            LLVMValueRef[] args = new LLVMValueRef[callCount + 1];

            args[0] = scan(mi.receiver);
            int i = 0;
            for (; i < fixedCount; i++) {
                Expression   arg    = mi.args[i];
                LLVMValueRef argVal = scan(arg);
                args[i + 1] = promote(arg.type, mi.funcSym.type.ParameterTypes[i], argVal);
            }

            // add varargs without promotion
            for (; i < callCount; i++) {
                Expression arg = mi.args[i];
                args[i + 1] = scan(arg);
            }

            string       name     = mi.type.IsVoid ? "" : mi.funcSym.name;
            LLVMValueRef callInst = LLVM.BuildCall(builder, mi.funcSym.llvmRef, args, name);
            return callInst;
        }

        public override LLVMValueRef visitThis(This @this)
        {
            return function.GetParam(0);
        }

        public override LLVMValueRef visitIdent(Identifier ident)
        {
            if (ident.symbol.owner.kind == Symbol.Kind.STRUCT) {
                LLVMValueRef left = function.GetParam(0);
                LLVMValueRef ptr =
                    LLVM.BuildStructGEP(builder, left, (uint)ident.symbol.LLVMFieldIndex, ident.name + ".ptr");
                return LLVM.BuildLoad(builder, ptr, ident.name);
            }

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

        public override LLVMValueRef visitNewStruct(NewStruct newStruct)
        {
            Symbol.StructSymbol sym = newStruct.symbol;

            LLVMValueRef metaPtr    = LLVM.BuildPointerCast(builder, sym.llvmMetaRef, PTR_VOID, "met");
            LLVMValueRef untypedPtr = LLVM.BuildCall(builder, allocFunction, new[] {metaPtr}, "tmp");

            return LLVM.BuildPointerCast(builder, untypedPtr, HEAP_PTR(sym.llvmTypeRef), "tmp");
        }

        public override LLVMValueRef visitNewArray(NewArray newArray)
        {
            ArrayType    arrayType  = (ArrayType)newArray.type;
            Expression   lengthExpr = newArray.length;
            LLVMValueRef length     = scan(lengthExpr);
            length = promote(lengthExpr.type, symtab.intType, length);

            LLVMValueRef metaPtr = LLVM.BuildPointerCast(builder, arrayType.llvmMetaRef, PTR_VOID, "met");

            LLVMValueRef untypedPtr = LLVM.BuildCall(builder, allocArrayFunction, new[] {metaPtr, length}, "tmp");
            return LLVM.BuildPointerCast(builder, untypedPtr, HEAP_PTR(arrayType.llvmType), "tmp");
        }

        public override LLVMValueRef visitLiteral(LiteralExpression literal)
        {
            switch (literal.typeTag) {
                case TypeTag.INT:
                    return CONST_INT32((int)literal.value);
                case TypeTag.LONG:
                    return CONST_INT64((long)literal.value);
                case TypeTag.FLOAT:
                    return CONST_FLOAT((float)literal.value);
                case TypeTag.DOUBLE:
                    return CONST_DOUBLE((double)literal.value);
                case TypeTag.BOOLEAN:
                    return LLVM.ConstInt(INT1, (ulong)((bool)literal.value ? 1 : 0), false);
                case TypeTag.CHAR:
                    return LLVM.ConstInt(INT8, (byte)literal.value, true);
                case TypeTag.C_STRING:
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
                case Identifier id:
                    if (id.symbol.owner.kind == Symbol.Kind.STRUCT) {
                        LLVMValueRef ptrToObject = function.GetParam(0);
                        LLVMValueRef ptrToField = LLVM.BuildStructGEP(builder, ptrToObject,
                            (uint)id.symbol.LLVMFieldIndex, id.name + ".ptr");
                        return ptrToField;
                    } else {
                        return id.symbol.llvmRef;
                    }
                case Select s: {
                    LLVMValueRef ptrToObject = scan(s.selectBase);
                    LLVMValueRef ptrToField = LLVM.BuildStructGEP(builder, ptrToObject,
                        (uint)s.symbol.LLVMFieldIndex, s.name + ".ptr");
                    return ptrToField;
                }
                case ArrayIndex ind:
                    LLVMValueRef index      = scan(ind.index);
                    LLVMValueRef ptrToArray = scan(ind.indexBase);
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
                LLVMValueRef one    = toLLVMConst(1, expr.operatorSym.type.ReturnType.Tag);
                LLVMValueRef resVal = LLVM.BuildBinOp(builder, expr.operatorSym.opcode, operandVal, one, "incDec");
                LLVM.BuildStore(builder, resVal, getPointerToLocation(expr.operand));
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
                return buildShortCircuitLogical(expr.opcode == Tag.OR, expr.left, expr.right);
            }

            return doBinary(expr.left, expr.right, expr.operatorSym);
        }

        private LLVMValueRef doBinary(Expression left, Expression right, Symbol.OperatorSymbol op)
        {
            LLVMValueRef leftVal  = scan(left);
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

        private LLVMValueRef buildShortCircuitLogical(bool isOR, Expression left, Expression right)
        {
            LLVMValueRef      leftVal   = scan(left);
            LLVMBasicBlockRef prevBlock = LLVM.GetInsertBlock(builder);

            LLVMBasicBlockRef rightBlock = LLVM.AppendBasicBlock(function, "right");
            LLVMBasicBlockRef afterIf    = LLVM.AppendBasicBlock(function, "afterSC");

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
