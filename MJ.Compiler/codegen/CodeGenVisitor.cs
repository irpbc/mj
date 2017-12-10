using System;

using LLVMSharp;

namespace mj.compiler.codegen
{
    /*public class CodeGenVisitor : AstVisitor<LLVMValueRef>
    {
        private LLVMBuilderRef builder;

        public CodeGenVisitor(LLVMBuilderRef builder)
        {
            this.builder = builder;
        }

        public override LLVMValueRef visitBinary(BinaryExpressionNode expr)
        {
            var left = Visit(expr.left);
            var right = Visit(expr.right);
            switch (expr.op) {
                case Operator.ADD: return LLVM.BuildFAdd(builder, left, right, "add");
                case Operator.SUB: return LLVM.BuildFSub(builder, left, right, "sub");
                case Operator.MUL: return LLVM.BuildFMul(builder, left, right, "mul");
                case Operator.DIV: return LLVM.BuildFDiv(builder, left, right, "div");
                case Operator.MOD: return LLVM.BuildFRem(builder, left, right, "rem");
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override LLVMValueRef visitUnary(UnaryExpressionNode expr)
        {
            switch (expr.op) {
                case Operator.ADD: return Visit(expr.operand);
                case Operator.SUB: return LLVM.BuildFNeg(builder, Visit(expr.operand), "neg");
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override LLVMValueRef visitLiteral(LiteralExpressionNode literal)
        {
            return LLVM.ConstReal(LLVMTypeRef.DoubleType(), (double)literal.value);
        }

        public override LLVMValueRef visitMethodDef(MethodNode method)
        {
            throw new NotImplementedException();
        }

        public override LLVMValueRef visitParam(MethodParameter parameter)
        {
            throw new NotImplementedException();
        }

        public override LLVMValueRef visitLocalVar(LocalVariableDeclaration localVariableDeclaration)
        {
            throw new NotImplementedException();
        }

        public override LLVMValueRef visitCompilationUnit(CompilatioUnit compilatioUnit)
        {
            throw new NotImplementedException();
        }

        public override LLVMValueRef visitIdent(IdentifierNode identifierNode)
        {
            throw new NotImplementedException();
        }

        public override LLVMValueRef visitMethodInvoke(MethodInvocation methodInvocation)
        {
            throw new NotImplementedException();
        }

        public override LLVMValueRef visitReturn(ReturnStatement returnStatement)
        {
            throw new NotImplementedException();
        }

        public override LLVMValueRef visitBlock(Block block)
        {
            throw new NotImplementedException();
        }

        public override LLVMValueRef visitBreak(Break @break)
        {
            throw new NotImplementedException();
        }

        public override LLVMValueRef visitIf(If @if)
        {
            throw new NotImplementedException();
        }

        public override LLVMValueRef visitContinue(Continue @continue)
        {
            throw new NotImplementedException();
        }

        public override LLVMValueRef visitWhile(WhileStatement whileStatement)
        {
            throw new NotImplementedException();
        }

        public override LLVMValueRef visitFor(ForLoop forLoop)
        {
            throw new NotImplementedException();
        }

        public override LLVMValueRef visitExpresionStmt(ExpressionStatement expressionStatement)
        {
            throw new NotImplementedException();
        }

        public override LLVMValueRef visitInvocation(MethodInvocation methodInvocation)
        {
            throw new NotImplementedException();
        }

        public override LLVMValueRef visitDo(DoStatement doStatement)
        {
            throw new NotImplementedException();
        }

        public override LLVMValueRef visitConditional(ConditionalExpressionNode conditionalExpressionNode)
        {
            throw new NotImplementedException();
        }

        public override LLVMValueRef visitPrimitiveType(PrimitiveTypeNode primitiveTypeNode)
        {
            throw new NotImplementedException();
        }
        
        private static void Llvm(ExpressionNode expression)
        {
            LLVMBool success = new LLVMBool(value: 0);
            LLVMModuleRef mod = LLVM.ModuleCreateWithName("LLVMSharpIntro");

            LLVMTypeRef retType = LLVM.FunctionType(LLVM.DoubleType(), ParamTypes: new LLVMTypeRef[0], IsVarArg: false);
            LLVMValueRef function = LLVM.AddFunction(mod, "function", retType);

            LLVMBasicBlockRef entry = LLVM.AppendBasicBlock(function, "entry");

            LLVMBuilderRef builder = LLVM.CreateBuilder();
            LLVM.PositionBuilderAtEnd(builder, entry);

            var codeGenVisitor = new CodeGenVisitor(builder);
            var finalValue = codeGenVisitor.Visit(expression);
            var ret = LLVM.BuildRet(builder, finalValue);

            if (LLVM.VerifyModule(mod, LLVMVerifierFailureAction.LLVMPrintMessageAction, out var error) != success) {
                Console.WriteLine($"Error: {error}");
                return;
            }

            LLVM.LinkInMCJIT();

            LLVM.InitializeX86TargetMC();
            LLVM.InitializeX86Target();
            LLVM.InitializeX86TargetInfo();
            LLVM.InitializeX86AsmParser();
            LLVM.InitializeX86AsmPrinter();

            LLVMMCJITCompilerOptions options = new LLVMMCJITCompilerOptions {NoFramePointerElim = 1, OptLevel = 0};
            LLVM.InitializeMCJITCompilerOptions(options);
            if (LLVM.CreateMCJITCompilerForModule(out var engine, mod, options, out error) != success) {
                Console.WriteLine($"Error: {error}");
            }

            var execute =
                (Execute)Marshal.GetDelegateForFunctionPointer(LLVM.GetPointerToGlobal(engine, function),
                    typeof(Execute));
            double result = execute();

            LLVM.DumpModule(mod);

            LLVM.DisposeBuilder(builder);
            LLVM.DisposeExecutionEngine(engine);

            Console.WriteLine($"Rezultat kompajliranog koda je: {result}");
        }
        
    }*/
}
