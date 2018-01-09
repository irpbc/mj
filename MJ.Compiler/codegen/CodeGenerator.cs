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

        private LLVMValueRef allocFunction;

        private static readonly LLVMValueRef nullValue = default(LLVMValueRef);

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
                Console.WriteLine("Cvrc");
                return;
            }

            string triple = getTargetTriple();

            LLVMTargetMachineRef targetMachine = LLVM.CreateTargetMachine(target, triple, "generic", "",
                LLVMCodeGenOptLevel.LLVMCodeGenLevelDefault, LLVMRelocMode.LLVMRelocDefault,
                LLVMCodeModel.LLVMCodeModelDefault);

            LLVM.TargetMachineEmitToMemoryBuffer(targetMachine, module, LLVMCodeGenFileType.LLVMObjectFile,
                out var targetDescription, out var memoryBuffer);

            Console.WriteLine(targetDescription);

            size_t bufferSize = LLVM.GetBufferSize(memoryBuffer);
            IntPtr bufferStart = LLVM.GetBufferStart(memoryBuffer);

            unsafe {
                Stream s = new UnmanagedMemoryStream((byte*)bufferStart.ToPointer(), bufferSize);
                FileInfo file = new FileInfo(options.OutPath);
                if (file.Exists) {
                    file.Delete();
                }

                using (FileStream fs = file.Open(FileMode.Create, FileAccess.Write))
                using (DisposableBuffer buffer = new DisposableBuffer(memoryBuffer)) {
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

        // C++ RTTI style object to wrap the unmanaged resource
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
                LLVMValueRef func = LLVM.AddFunction(module, "mj_" + m.name, typeResolver.resolve(m.type));
                func.SetLinkage(LLVMLinkage.LLVMExternalLinkage);
                m.llvmPointer = func;
            }
        }

        public void declareRuntimeHelpers()
        {
            LLVMTypeRef retType = LLVM.PointerType(LLVM.Int8Type(), 0);
            LLVMTypeRef[] args = {LLVM.Int32Type()};
            allocFunction = LLVM.AddFunction(module, "mjrt_alloc", LLVM.FunctionType(retType, args, false));
            allocFunction.SetLinkage(LLVMLinkage.LLVMExternalLinkage);
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
            foreach (Tree decl in compilationUnit.declarations) {
                switch (decl) {
                    case MethodDef mt:
                        createFunction(mt);
                        break;
                    case ClassDef cd:
                        createClass(cd);
                        break;
                    case AspectDef asp:
                        createFunction(asp.after);
                        break;
                }
            }
        }

        private void createClass(ClassDef cd)
        {
            Symbol.ClassSymbol sym = cd.symbol;
  
            LLVMTypeRef[] llvmTypes = cd.members
                                        .OfType<VariableDeclaration>()
                                        .Select(f => typeResolver.resolve(f.symbol.type)).ToArray();

            LLVMTypeRef structType = LLVM.StructCreateNamed(context, cd.name);
            structType.StructSetBody(llvmTypes, false);

            sym.llvmPointer = structType;
        }

        private void createFunction(MethodDef mt)
        {
            LLVMTypeRef llvmType = typeResolver.resolve(mt.symbol.type);
            LLVMValueRef func = LLVM.AddFunction(module, mt.name, llvmType);
            func.SetFunctionCallConv((uint)LLVMCallConv.LLVMCCallConv);

            mt.symbol.llvmPointer = func;
        }

        public override LLVMValueRef visitClassDef(ClassDef classDef) => nullValue;

        public override LLVMValueRef visitMethodDef(MethodDef mt)
        {
            function = mt.symbol.llvmPointer;

            LLVMBasicBlockRef entryBlock = function.AppendBasicBlock("entry");
            LLVM.PositionBuilderAtEnd(builder, entryBlock);

            variableAllocator.scan(mt.body);

            LLVMValueRef[] llvmParams = function.GetParams();
            for (var i = 0; i < llvmParams.Length; i++) {
                mt.symbol.parameters[i].llvmPointer = llvmParams[i];
            }

            method = mt;
            scan(mt.body.statements);

            endFunction();
            return nullValue;
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
                value = promote((PrimitiveType)returnStatement.value.type,
                    (PrimitiveType)method.symbol.type.ReturnType,
                    value);
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
                LLVM.BuildStore(builder, initVal, varDef.symbol.llvmPointer);
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
                args[i] = promote((PrimitiveType)arg.type, (PrimitiveType)mi.methodSym.type.ParameterTypes[i],
                    argVal);
            }

            // add varargs without promotion
            for (; i < callCount; i++) {
                Expression arg = mi.args[i];
                args[i] = scan(arg);
            }

            string name = mi.type.IsVoid ? "" : mi.methodSym.name;
            LLVMValueRef callInst = LLVM.BuildCall(builder, mi.methodSym.llvmPointer, args, name);
            return callInst;
        }

        public override LLVMValueRef visitIdent(Identifier ident)
        {
            if (ident.symbol.kind == Symbol.Kind.PARAM) {
                // parameters are direct values
                return ident.symbol.llvmPointer;
            }

            // local variables are "allocated"
            return LLVM.BuildLoad(builder, ident.symbol.llvmPointer, "load." + ident.name);
        }

        public override LLVMValueRef visitSelect(Select select)
        {
            LLVMValueRef left = scan(select.selected);
            LLVMValueRef ptr =
                LLVM.BuildStructGEP(builder, left, (uint)select.symbol.fieldIndex, select.name + ".ptr");
            return LLVM.BuildLoad(builder, ptr, select.name);
        }

        public override LLVMValueRef visitNewClass(NewClass newClass)
        {
            LLVMTypeRef structType = newClass.symbol.llvmPointer;
            LLVMTypeRef ptrToStruct = LLVM.PointerType(structType, 0);

            LLVMValueRef gep = LLVM.BuildGEP(builder, LLVM.ConstNull(ptrToStruct), new[] {const1(TypeTag.INT)}, "tmp");
            LLVMValueRef size = LLVM.BuildPtrToInt(builder, gep, LLVM.Int32Type(), "size");

            LLVMValueRef untypedPtr = LLVM.BuildCall(builder, allocFunction, new[] {size}, "tmp");
            return LLVM.BuildPointerCast(builder, untypedPtr, ptrToStruct, newClass.symbol.name);
        }

        public override LLVMValueRef visitLiteral(LiteralExpression literal)
        {
            switch (literal.typeTag) {
                case TypeTag.INT:
                    return LLVM.ConstInt(LLVMTypeRef.Int32Type(), (ulong)(int)literal.value, new LLVMBool(1));
                case TypeTag.LONG:
                    return LLVM.ConstInt(LLVMTypeRef.Int32Type(), (ulong)(long)literal.value, new LLVMBool(1));
                case TypeTag.FLOAT:
                    return LLVM.ConstReal(LLVMTypeRef.FloatType(), (float)literal.value);
                case TypeTag.DOUBLE:
                    return LLVM.ConstReal(LLVMTypeRef.DoubleType(), (double)literal.value);
                case TypeTag.BOOLEAN:
                    return LLVM.ConstInt(LLVMTypeRef.Int1Type(), (ulong)((bool)literal.value ? 1 : 0),
                        new LLVMBool(0));
                case TypeTag.STRING:
                    LLVMValueRef val = LLVM.BuildGlobalStringPtr(builder, (string)literal.value, "strLit");
                    return val;
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
                case Identifier id: return id.symbol.llvmPointer;
                case Select s:
                    LLVMValueRef ptrToPtrToObject = getPointerToLocation(s.selected);
                    LLVMValueRef ptrToObject = LLVM.BuildLoad(builder, ptrToPtrToObject, "tmp");
                    LLVMValueRef ptrToField = LLVM.BuildStructGEP(builder, ptrToObject, 
                        (uint)s.symbol.fieldIndex, s.name + ".ptr");
                    
                    return ptrToField;
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
                LLVMValueRef one = const1(expr.operatorSym.type.ReturnType.Tag);
                LLVMValueRef resVal = LLVM.BuildBinOp(builder, expr.operatorSym.opcode, operandVal, one, "incDec");
                LLVM.BuildStore(builder, resVal, ((Identifier)expr.operand).symbol.llvmPointer);
                return expr.opcode.isPre() ? resVal : operandVal;
            }

            throw new ArgumentOutOfRangeException();
        }

        private LLVMValueRef const1(TypeTag type)
        {
            switch (type) {
                case TypeTag.INT:
                    return LLVM.ConstInt(LLVMTypeRef.Int32Type(), 1, new LLVMBool(0));
                case TypeTag.LONG:
                    return LLVM.ConstInt(LLVMTypeRef.Int64Type(), 1, new LLVMBool(0));
                case TypeTag.FLOAT:
                    return LLVM.ConstReal(LLVMTypeRef.FloatType(), 1);
                case TypeTag.DOUBLE:
                    return LLVM.ConstReal(LLVMTypeRef.DoubleType(), 1);
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

            LLVMValueRef phi = LLVM.BuildPhi(builder, LLVMTypeRef.Int1Type(), "res");
            
            LLVMValueRef phiLeftVal = LLVM.ConstInt(LLVMTypeRef.Int1Type(), (ulong)(isOR ? 1 : 0), new LLVMBool(0));

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
