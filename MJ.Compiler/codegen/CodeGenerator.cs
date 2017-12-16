using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using LLVMSharp;

using mj.compiler.symbol;
using mj.compiler.tree;
using mj.compiler.utils;

namespace mj.compiler.codegen
{
    public class CodeGenerator : AstVisitor<LLVMValueRef>
    {
        private static readonly Context.Key<CodeGenerator> CONTEXT_KEY = new Context.Key<CodeGenerator>();

        public static CodeGenerator instance(Context ctx) =>
            ctx.tryGet(CONTEXT_KEY, out var instance) ? instance : new CodeGenerator(ctx);

        private LLVMBuilderRef builder;
        private LLVMValueRef function;
        private LLVMModuleRef module;

        private readonly LLVMTypeResolver typeResolver;
        private VariableAllocator variableAllocator;
        private readonly JumpPatcher jumpPatcher;
        private readonly CallPatcher callPatcher;
        private readonly Typings typings;

        public CodeGenerator(Context ctx)
        {
            ctx.put(CONTEXT_KEY, this);

            typings = Typings.instance(ctx);
            jumpPatcher = new JumpPatcher();
            callPatcher = new CallPatcher();
            typeResolver = new LLVMTypeResolver();
        }

        [UnmanagedFunctionPointer(CallingConvention.FastCall)]
        private delegate int MainFunction();

        public void main(IList<CompilationUnit> trees)
        {
            module = LLVM.ModuleCreateWithName("TheProgram");
            builder = LLVM.CreateBuilder();

            variableAllocator = new VariableAllocator(builder, typeResolver);

            scan(trees);
            callPatcher.scan(trees);

            LLVMBool success = new LLVMBool(value: 0);

            if (LLVM.VerifyModule(module, LLVMVerifierFailureAction.LLVMPrintMessageAction, out var error) != success) {
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
            if (LLVM.CreateMCJITCompilerForModule(out var engine, module, options, out error) != success) {
                Console.WriteLine($"Error: {error}");
            }

            LLVMValueRef mainFunction = LLVM.GetNamedFunction(module, "main");
            var execute = (MainFunction)Marshal.GetDelegateForFunctionPointer(
                LLVM.GetPointerToGlobal(engine, mainFunction), typeof(MainFunction));

            int result = execute();

            Console.WriteLine($"Program excuted with result: {result}");

            LLVM.DumpModule(module);
            LLVM.DisposeBuilder(builder);
            LLVM.DisposeExecutionEngine(engine);
        }

        public override LLVMValueRef visitCompilationUnit(CompilationUnit compilationUnit)
        {
            foreach (MethodDef mt in compilationUnit.methods) {
                LLVMTypeRef llvmType = mt.symbol.type.accept(typeResolver);
                function = LLVM.AddFunction(module, mt.name, llvmType);
                function.SetFunctionCallConv((uint)LLVMCallConv.LLVMFastCallConv);
                mt.symbol.llvmPointer = function;

                LLVMBasicBlockRef entryBlock = function.AppendBasicBlock("entry");
                LLVM.PositionBuilderAtEnd(builder, entryBlock);

                variableAllocator.scan(mt.body);

                scan(mt.body.statements);
                jumpPatcher.scan(mt.body.statements);
            }

            return default(LLVMValueRef);
        }

        public override LLVMValueRef visitBlock(Block block)
        {
            scan(block.statements);
            return default(LLVMValueRef);
        }

        public override LLVMValueRef visitIf(If ifStat)
        {
            LLVMValueRef conditionVal = scan(ifStat.condition);

            LLVMBasicBlockRef thenBlock = LLVM.AppendBasicBlock(function, "then");

            if (ifStat.elsePart == null) {
                LLVMBasicBlockRef afterIf = LLVM.AppendBasicBlock(function, "afterIf");

                LLVM.BuildCondBr(builder, conditionVal, thenBlock, afterIf);
                LLVM.PositionBuilderAtEnd(builder, thenBlock);
                scan(ifStat.thenPart);
                LLVM.PositionBuilderAtEnd(builder, afterIf);
            } else {
                LLVMBasicBlockRef elseBlock = LLVM.AppendBasicBlock(function, "else");
                LLVM.BuildCondBr(builder, conditionVal, thenBlock, elseBlock);
                LLVM.PositionBuilderAtEnd(builder, thenBlock);
                scan(ifStat.thenPart);
                LLVM.PositionBuilderAtEnd(builder, elseBlock);
                scan(ifStat.elsePart);
                LLVMBasicBlockRef afterIf = LLVM.AppendBasicBlock(function, "afterIf");
                LLVM.PositionBuilderAtEnd(builder, afterIf);
            }

            return default(LLVMValueRef);
        }

        public override LLVMValueRef visitWhile(WhileStatement whileStat)
        {
            LLVMBasicBlockRef whileBlock = LLVM.AppendBasicBlock(function, "while");
            LLVMValueRef conditionVal = scan(whileStat.condition);
            LLVMBasicBlockRef thenBlock = LLVM.AppendBasicBlock(function, "then");
            LLVMBasicBlockRef afterWhile = LLVM.AppendBasicBlock(function, "afterWhile");
            LLVM.BuildCondBr(builder, conditionVal, thenBlock, afterWhile);

            LLVM.PositionBuilderAtEnd(builder, thenBlock);
            scan(whileStat.body);
            LLVM.BuildBr(builder, whileBlock);

            LLVM.PositionBuilderAtEnd(builder, afterWhile);

            whileStat.ContinueBlock = whileBlock;
            whileStat.BreakBlock = afterWhile;

            return default(LLVMValueRef);
        }

        public override LLVMValueRef visitDo(DoStatement doStat)
        {
            LLVMBasicBlockRef doBlock = LLVM.AppendBasicBlock(function, "do");
            LLVMBasicBlockRef afterDo = LLVM.AppendBasicBlock(function, "afterDo");

            LLVM.PositionBuilderAtEnd(builder, doBlock);
            scan(doStat.body);
            LLVMValueRef conditionVal = scan(doStat.condition);
            LLVM.BuildCondBr(builder, conditionVal, doBlock, afterDo);

            LLVM.PositionBuilderAtEnd(builder, afterDo);

            doStat.ContinueBlock = doBlock;
            doStat.BreakBlock = afterDo;

            return default(LLVMValueRef);
        }

        public override LLVMValueRef visitFor(ForLoop forLoop)
        {
            scan(forLoop.init);
            LLVMBasicBlockRef forBlock = function.AppendBasicBlock("for");
            LLVMBasicBlockRef afterFor = function.AppendBasicBlock("afterfor");

            LLVM.PositionBuilderAtEnd(builder, forBlock);
            if (forLoop.condition != null) {
                LLVMValueRef condValue = scan(forLoop.condition);
                LLVMBasicBlockRef thenBlock = function.AppendBasicBlock("then");
                LLVM.BuildCondBr(builder, condValue, thenBlock, afterFor);
                LLVM.PositionBuilderAtEnd(builder, thenBlock);
            }
            scan(forLoop.body);

            scan(forLoop.update);
            LLVM.BuildBr(builder, forBlock);
            LLVM.PositionBuilderAtEnd(builder, afterFor);

            forLoop.ContinueBlock = forBlock;
            forLoop.BreakBlock = afterFor;

            return default(LLVMValueRef);
        }

        public override LLVMValueRef visitSwitch(Switch @switch)
        {
            LLVMValueRef selectorVal = scan(@switch.selector);
            int numCases = @switch.cases.Count;
            if (numCases == 0) {
                return default(LLVMValueRef);
            }

            Case defaultCase = null;

            for (var index = 0; index < @switch.cases.Count; index++) {
                Case @case = @switch.cases[index];
                if (@case.expression == null) {
                    defaultCase = @case;
                    break;
                }
            }

            LLVMBasicBlockRef[] caseBlocks = new LLVMBasicBlockRef[numCases];
            LLVMBasicBlockRef? elseBlock = null;

            for (var i = 0; i < caseBlocks.Length; i++) {
                Case @case = @switch.cases[i];
                LLVMBasicBlockRef caseBlock = function.AppendBasicBlock("case");
                LLVM.PositionBuilderAtEnd(builder, caseBlock);
                scan(@case.statements);
                if (@case.expression != null) {
                    caseBlocks[i] = caseBlock;
                } else {
                    elseBlock = caseBlock;
                }
            }

            LLVMBasicBlockRef afterSwitch = function.AppendBasicBlock("afterSwitch");
            if (elseBlock == null) {
                elseBlock = afterSwitch;
            }

            LLVMValueRef switchInst = LLVM.BuildSwitch(builder, selectorVal, elseBlock.Value, (uint)numCases);

            for (var i = 0; i < caseBlocks.Length; i++) {
                Case @case = @switch.cases[i];
                if (@case != null) {
                    LLVMBasicBlockRef llvmBasicBlockRef = caseBlocks[i];
                    LLVMValueRef caseVal = scan(@case.expression);
                    Assert.assert(caseVal.IsConstant());
                    switchInst.AddCase(caseVal, llvmBasicBlockRef);
                }
            }

            @switch.BreakBlock = afterSwitch;

            return default(LLVMValueRef);
        }

        public override LLVMValueRef visitBreak(Break @break)
        {
            // Write dummy instruction, and remember it for later patching
            @break.instruction = LLVM.BuildBr(builder, default(LLVMValueRef));
            return default(LLVMValueRef);
        }

        public override LLVMValueRef visitContinue(Continue @continue)
        {
            // Write dummy instruction, and remember it for later patching
            @continue.instruction = LLVM.BuildBr(builder, default(LLVMValueRef));
            return default(LLVMValueRef);
        }

        public override LLVMValueRef visitReturn(ReturnStatement returnStatement)
        {
            if (returnStatement.value == null) {
                LLVM.BuildRetVoid(builder);
            } else {
                LLVM.BuildRet(builder, scan(returnStatement.value));
            }
            return default(LLVMValueRef);
        }

        public override LLVMValueRef visitExpresionStmt(ExpressionStatement expr)
        {
            scan(expr.expression);
            return default(LLVMValueRef);
        }

        public override LLVMValueRef visitVarDef(VariableDeclaration varDef)
        {
            if (varDef.init != null) {
                LLVM.BuildStore(builder, scan(varDef.init), varDef.symbol.llvmPointer);
            }
            return default(LLVMValueRef);
        }

        public override LLVMValueRef visitConditional(ConditionalExpression conditional)
        {
            LLVMValueRef conditionVal = scan(conditional.condition);

            LLVMBasicBlockRef thenBlock = LLVM.AppendBasicBlock(function, "then");
            LLVMBasicBlockRef elseBlock = LLVM.AppendBasicBlock(function, "else");

            LLVM.BuildCondBr(builder, conditionVal, thenBlock, elseBlock);

            LLVM.PositionBuilderAtEnd(builder, thenBlock);
            LLVMValueRef trueVal = scan(conditional.ifTrue);

            LLVM.PositionBuilderAtEnd(builder, elseBlock);
            LLVMValueRef falseVal = scan(conditional.ifFalse);

            LLVMBasicBlockRef afterIf = LLVM.AppendBasicBlock(function, "afterIf");
            LLVM.PositionBuilderAtEnd(builder, afterIf);

            LLVMValueRef phi = LLVM.BuildPhi(builder, conditional.type.accept(typeResolver), "cond");
            phi.AddIncoming(new[] {trueVal, falseVal}, new[] {thenBlock, elseBlock}, 2);
            return phi;
        }

        public override LLVMValueRef visitMethodInvoke(MethodInvocation methodInvocation)
        {
            LLVMValueRef[] args = new LLVMValueRef[methodInvocation.args.Count];
            for (var i = 0; i < methodInvocation.args.Count; i++) {
                args[i] = scan(methodInvocation.args[i]);
            }

            // The target method might not be genearted yet, so we dont supply 
            // the function, but save the instruction for later patching.
            LLVMValueRef callInst =
                LLVM.BuildCall(builder, default(LLVMValueRef), args, methodInvocation.methodSym.name);
            methodInvocation.instruction = callInst;
            return callInst;
        }

        public override LLVMValueRef visitIdent(Identifier ident)
        {
            return LLVM.BuildLoad(builder, ident.symbol.llvmPointer, "load." + ident.name);
        }

        public override LLVMValueRef visitLiteral(LiteralExpression literal)
        {
            switch (literal.type) {
                case TypeTag.INT:
                    return LLVM.ConstInt(LLVMTypeRef.Int32Type(), (ulong)(int)literal.value, new LLVMBool(1));
                case TypeTag.LONG:
                    return LLVM.ConstInt(LLVMTypeRef.Int32Type(), (ulong)(long)literal.value, new LLVMBool(1));
                case TypeTag.FLOAT:
                    return LLVM.ConstReal(LLVMTypeRef.FloatType(), (float)literal.value);
                case TypeTag.DOUBLE:
                    return LLVM.ConstReal(LLVMTypeRef.DoubleType(), (double)literal.value);
                case TypeTag.BOOLEAN:
                    return LLVM.ConstInt(LLVMTypeRef.Int1Type(), (ulong)((bool)literal.value ? 1 : 0), new LLVMBool(0));
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override LLVMValueRef visitAssign(AssignNode expr)
        {
            LLVMValueRef assignValue = scan(expr.right);
            Identifier ident = (Identifier)expr.left;

            doAssign(ident, assignValue);

            return assignValue;
        }

        public override LLVMValueRef visitCompoundAssign(CompoundAssignNode expr)
        {
            LLVMValueRef resValue = doBinary(expr.left, expr.right, expr.operatorSym);

            doAssign((Identifier)expr.left, resValue);

            return resValue;
        }

        private void doAssign(Identifier ident, LLVMValueRef value)
        {
            LLVM.BuildStore(builder, value, ident.symbol.llvmPointer);
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
            return doBinary(expr.left, expr.right, expr.operatorSym);
        }

        private LLVMValueRef doBinary(Expression left, Expression right, Symbol.OperatorSymbol op)
        {
            LLVMValueRef leftVal = scan(left);
            LLVMValueRef rightVal = scan(right);

            if (op.type.ReturnType.IsNumeric) {
                leftVal =
                    promote((PrimitiveType)left.type, (PrimitiveType)op.type.ParameterTypes[0], leftVal);
                rightVal =
                    promote((PrimitiveType)right.type, (PrimitiveType)op.type.ParameterTypes[1], rightVal);
            }

            return makeBinary(op, leftVal, rightVal);
        }

        public LLVMValueRef makeBinary(Symbol.OperatorSymbol binary, LLVMValueRef leftVal, LLVMValueRef rightVal)
        {
            LLVMOpcode llvmOpcode = binary.opcode;
            if (llvmOpcode == LLVMOpcode.LLVMICmp) {
                LLVM.BuildICmp(builder, (LLVMIntPredicate)binary.llvmPredicate, leftVal, rightVal, "icmp");
            } else if (llvmOpcode == LLVMOpcode.LLVMICmp) {
                LLVM.BuildFCmp(builder, (LLVMRealPredicate)binary.llvmPredicate, leftVal, rightVal, "fcmp");
            }

            return LLVM.BuildBinOp(builder, llvmOpcode, leftVal, rightVal, "tmp");
        }

        public LLVMValueRef promote(PrimitiveType actualType, PrimitiveType requestedType, LLVMValueRef value)
        {
            if (actualType == requestedType) {
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
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
