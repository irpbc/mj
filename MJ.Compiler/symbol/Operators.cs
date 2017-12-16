using System.Collections.Generic;

using Antlr4.Runtime.Atn;

using LLVMSharp;

using mj.compiler.main;
using mj.compiler.resources;
using mj.compiler.tree;
using mj.compiler.utils;

using static LLVMSharp.LLVMIntPredicate;
using static LLVMSharp.LLVMOpcode;
using static LLVMSharp.LLVMRealPredicate;

using static mj.compiler.symbol.Symbol;
using static mj.compiler.tree.Tag;

namespace mj.compiler.symbol
{
    public class Operators
    {
        private static readonly Context.Key<Operators> CONTEXT_KEY = new Context.Key<Operators>();

        public static Operators instance(Context ctx) =>
            ctx.tryGet(CONTEXT_KEY, out var instance) ? instance : new Operators(ctx);

        private readonly Symtab symtab;
        private readonly Log log;
        private readonly Typings typings;

        private Operators(Context ctx)
        {
            ctx.put(CONTEXT_KEY, this);

            symtab = Symtab.instance(ctx);
            log = Log.instance(ctx);
            typings = Typings.instance(ctx);

            initOperators();
        }

        private readonly string[] operatorNames = new string[MOD_ASG - NEG + 1];

        private readonly IDictionary<Tag, IList<OperatorSymbol>> unaryOperators =
            new Dictionary<Tag, IList<OperatorSymbol>>();

        private readonly IDictionary<Tag, IList<OperatorSymbol>> binaryOperators =
            new Dictionary<Tag, IList<OperatorSymbol>>();

        public OperatorSymbol resolveUnary(DiagnosticPosition pos, Tag tag, Type argType)
        {
            if (argType.IsError) {
                return symtab.errorOpSymbol;
            }
            IList<OperatorSymbol> operators = unaryOperators[tag];
            for (var i = 0; i < operators.Count; i++) {
                OperatorSymbol op = operators[i];
                // We check for exact match for unary operators
                if (op.type.ParameterTypes[0] == argType) {
                    return op;
                }
            }
            log.error(pos, messages.unresolvedUnaryOperator, operatorNames[(int)tag], argType);
            return symtab.errorOpSymbol;
        }

        public OperatorSymbol resolveBinary(DiagnosticPosition pos, Tag tag, Type leftType, Type rightType)
        {
            if (leftType.IsError || rightType.IsError) {
                return symtab.errorOpSymbol;
            }
            IList<OperatorSymbol> operators = binaryOperators[tag];
            for (var i = 0; i < operators.Count; i++) {
                OperatorSymbol op = operators[i];
                IList<Type> paramTypes = op.type.ParameterTypes;
                // We take numeric promotion into account
                if (typings.isAssignableFrom(paramTypes[0], leftType) &&
                    typings.isAssignableFrom(paramTypes[1], rightType)) {
                    return op;
                }
            }
            log.error(pos, messages.unresolvedBinaryOperator, operatorNames[tag.operatorIndex()], leftType, rightType);
            return symtab.errorOpSymbol;
        }

        private void initOperators()
        {
            setName(NEG, "-");
            unaryOperators.Add(NEG, new[] {
                unary(NEG, TypeTag.INT, TypeTag.INT),
                unary(NEG, TypeTag.LONG, TypeTag.LONG),
                unary(NEG, TypeTag.FLOAT, TypeTag.DOUBLE),
                unary(NEG, TypeTag.DOUBLE, TypeTag.DOUBLE),
            });

            setName(NOT, "!");
            unaryOperators.Add(NOT, new[] {
                unary(NOT, TypeTag.BOOLEAN, TypeTag.BOOLEAN)
            });

            setName(COMPL, "~");
            unaryOperators.Add(COMPL, new[] {
                unary(COMPL, TypeTag.INT, TypeTag.INT),
                unary(COMPL, TypeTag.LONG, TypeTag.LONG)
            });

            setName(PRE_INC, "++");
            unaryOperators.Add(PRE_INC, new[] {
                unary(PRE_INC, TypeTag.INT, TypeTag.INT, LLVMAdd),
                unary(PRE_INC, TypeTag.LONG, TypeTag.LONG, LLVMAdd),
                unary(PRE_INC, TypeTag.FLOAT, TypeTag.FLOAT, LLVMFAdd),
                unary(PRE_INC, TypeTag.DOUBLE, TypeTag.DOUBLE, LLVMFAdd)
            });

            setName(PRE_DEC, "--");
            unaryOperators.Add(PRE_DEC, new[] {
                unary(PRE_DEC, TypeTag.INT, TypeTag.INT, LLVMSub),
                unary(PRE_DEC, TypeTag.LONG, TypeTag.LONG, LLVMSub),
                unary(PRE_DEC, TypeTag.FLOAT, TypeTag.FLOAT, LLVMFSub),
                unary(PRE_DEC, TypeTag.DOUBLE, TypeTag.DOUBLE, LLVMFSub)
            });

            setName(POST_INC, "++");
            unaryOperators.Add(POST_INC, new[] {
                unary(POST_INC, TypeTag.INT, TypeTag.INT, LLVMAdd),
                unary(POST_INC, TypeTag.LONG, TypeTag.LONG, LLVMAdd),
                unary(POST_INC, TypeTag.FLOAT, TypeTag.FLOAT, LLVMFAdd),
                unary(POST_INC, TypeTag.DOUBLE, TypeTag.DOUBLE, LLVMFAdd)
            });

            setName(POST_DEC, "--");
            unaryOperators.Add(POST_DEC, new[] {
                unary(POST_DEC, TypeTag.INT, TypeTag.INT, LLVMSub),
                unary(POST_DEC, TypeTag.LONG, TypeTag.LONG, LLVMSub),
                unary(POST_DEC, TypeTag.FLOAT, TypeTag.FLOAT, LLVMFSub),
                unary(POST_DEC, TypeTag.DOUBLE, TypeTag.DOUBLE, LLVMFSub)
            });

            setName(OR, "||");
            binaryOperators.Add(OR, new[] {
                binary(OR, TypeTag.BOOLEAN, TypeTag.BOOLEAN, TypeTag.BOOLEAN, LLVMOr),
            });

            setName(AND, "&&");
            binaryOperators.Add(AND, new[] {
                binary(AND, TypeTag.BOOLEAN, TypeTag.BOOLEAN, TypeTag.BOOLEAN, LLVMAnd),
            });

            // Order of combination listing for binary operators matters for correct resolution
            // More assignable types must be listed after less assignable ones,
            // which is the order listed in the TypeTag enum.

            setName(BITOR, "|");
            binaryOperators.Add(BITOR, new[] {
                binary(BITOR, TypeTag.BOOLEAN, TypeTag.BOOLEAN, TypeTag.BOOLEAN, LLVMOr),
                binary(BITOR, TypeTag.INT, TypeTag.INT, TypeTag.INT, LLVMOr),
                binary(BITOR, TypeTag.LONG, TypeTag.LONG, TypeTag.LONG, LLVMOr),
            });

            setName(BITXOR, "^");
            binaryOperators.Add(BITXOR, new[] {
                binary(BITXOR, TypeTag.BOOLEAN, TypeTag.BOOLEAN, TypeTag.BOOLEAN, LLVMXor),
                binary(BITXOR, TypeTag.INT, TypeTag.INT, TypeTag.INT, LLVMXor),
                binary(BITXOR, TypeTag.LONG, TypeTag.LONG, TypeTag.LONG, LLVMXor),
            });

            setName(BITAND, "&");
            binaryOperators.Add(BITAND, new[] {
                binary(BITAND, TypeTag.BOOLEAN, TypeTag.BOOLEAN, TypeTag.BOOLEAN, LLVMAnd),
                binary(BITAND, TypeTag.INT, TypeTag.INT, TypeTag.INT, LLVMAnd),
                binary(BITAND, TypeTag.LONG, TypeTag.LONG, TypeTag.LONG, LLVMAnd),
            });

            setName(EQ, "==");
            binaryOperators.Add(EQ, new[] {
                binary(EQ, TypeTag.BOOLEAN, TypeTag.BOOLEAN, TypeTag.BOOLEAN, LLVMICmp, LLVMIntEQ),
                binary(EQ, TypeTag.INT, TypeTag.INT, TypeTag.BOOLEAN, LLVMICmp, LLVMIntEQ),
                binary(EQ, TypeTag.LONG, TypeTag.LONG, TypeTag.BOOLEAN, LLVMICmp, LLVMIntEQ),
                binary(EQ, TypeTag.FLOAT, TypeTag.FLOAT, TypeTag.BOOLEAN, LLVMFCmp, LLVMRealOEQ),
                binary(EQ, TypeTag.DOUBLE, TypeTag.DOUBLE, TypeTag.BOOLEAN, LLVMFCmp, LLVMRealOEQ),
            });

            setName(NEQ, "!=");
            binaryOperators.Add(NEQ, new[] {
                binary(NEQ, TypeTag.BOOLEAN, TypeTag.BOOLEAN, TypeTag.BOOLEAN, LLVMICmp),
                binary(NEQ, TypeTag.INT, TypeTag.INT, TypeTag.BOOLEAN, LLVMICmp, LLVMIntNE),
                binary(NEQ, TypeTag.LONG, TypeTag.LONG, TypeTag.BOOLEAN, LLVMICmp, LLVMIntNE),
                binary(NEQ, TypeTag.FLOAT, TypeTag.FLOAT, TypeTag.BOOLEAN, LLVMFCmp, LLVMRealONE),
                binary(NEQ, TypeTag.DOUBLE, TypeTag.DOUBLE, TypeTag.BOOLEAN, LLVMFCmp, LLVMRealONE),
            });

            setName(LT, "<");
            binaryOperators.Add(LT, new[] {
                binary(LT, TypeTag.INT, TypeTag.INT, TypeTag.BOOLEAN, LLVMICmp, LLVMIntSLT),
                binary(LT, TypeTag.LONG, TypeTag.LONG, TypeTag.BOOLEAN, LLVMICmp, LLVMIntSLT),
                binary(LT, TypeTag.FLOAT, TypeTag.FLOAT, TypeTag.BOOLEAN, LLVMFCmp, LLVMRealOLT),
                binary(LT, TypeTag.DOUBLE, TypeTag.DOUBLE, TypeTag.BOOLEAN, LLVMFCmp, LLVMRealOLT),
            });

            setName(GT, ">");
            binaryOperators.Add(GT, new[] {
                binary(GT, TypeTag.INT, TypeTag.INT, TypeTag.BOOLEAN, LLVMICmp, LLVMIntSGT),
                binary(GT, TypeTag.LONG, TypeTag.LONG, TypeTag.BOOLEAN, LLVMICmp, LLVMIntSGT),
                binary(GT, TypeTag.FLOAT, TypeTag.FLOAT, TypeTag.BOOLEAN, LLVMFCmp, LLVMRealOGT),
                binary(GT, TypeTag.DOUBLE, TypeTag.DOUBLE, TypeTag.BOOLEAN, LLVMFCmp, LLVMRealOGT),
            });

            setName(LE, "<=");
            binaryOperators.Add(LE, new[] {
                binary(LE, TypeTag.INT, TypeTag.INT, TypeTag.BOOLEAN, LLVMICmp, LLVMIntSLE),
                binary(LE, TypeTag.LONG, TypeTag.LONG, TypeTag.BOOLEAN, LLVMICmp, LLVMIntSLE),
                binary(LE, TypeTag.FLOAT, TypeTag.FLOAT, TypeTag.BOOLEAN, LLVMFCmp, LLVMRealOLE),
                binary(LE, TypeTag.DOUBLE, TypeTag.DOUBLE, TypeTag.BOOLEAN, LLVMFCmp, LLVMRealOLE),
            });

            setName(GE, ">=");
            binaryOperators.Add(GE, new[] {
                binary(GE, TypeTag.INT, TypeTag.INT, TypeTag.BOOLEAN, LLVMICmp, LLVMIntSGE),
                binary(GE, TypeTag.LONG, TypeTag.LONG, TypeTag.BOOLEAN, LLVMICmp, LLVMIntSGE),
                binary(GE, TypeTag.FLOAT, TypeTag.FLOAT, TypeTag.BOOLEAN, LLVMFCmp, LLVMRealOGE),
                binary(GE, TypeTag.DOUBLE, TypeTag.DOUBLE, TypeTag.BOOLEAN, LLVMFCmp, LLVMRealOGE),
            });
            //@formatter:off

            setName(SHL, "<<");
            binaryOperators.Add(SHL, new[] {
                binary(SHL, TypeTag.INT, TypeTag.INT, TypeTag.INT, LLVMShl),
                binary(SHL, TypeTag.INT, TypeTag.LONG, TypeTag.INT, LLVMShl),
                binary(SHL, TypeTag.LONG, TypeTag.INT, TypeTag.LONG, LLVMShl),
                binary(SHL, TypeTag.LONG, TypeTag.LONG, TypeTag.LONG, LLVMShl),
            });

            setName(SHR, ">>");
            binaryOperators.Add(SHR, new[] {
                binary(SHR, TypeTag.INT, TypeTag.INT, TypeTag.INT, LLVMLShr),
                binary(SHR, TypeTag.INT, TypeTag.LONG, TypeTag.INT, LLVMLShr),
                binary(SHR, TypeTag.LONG, TypeTag.INT, TypeTag.LONG, LLVMLShr),
                binary(SHR, TypeTag.LONG, TypeTag.LONG, TypeTag.LONG, LLVMLShr),
            });

            setName(PLUS, "+");
            binaryOperators.Add(PLUS, new[] {
                binary(PLUS, TypeTag.INT, TypeTag.INT, TypeTag.INT, LLVMAdd),
                binary(PLUS, TypeTag.LONG, TypeTag.LONG, TypeTag.LONG, LLVMAdd),
                binary(PLUS, TypeTag.FLOAT, TypeTag.FLOAT, TypeTag.FLOAT, LLVMFAdd),
                binary(PLUS, TypeTag.DOUBLE, TypeTag.DOUBLE, TypeTag.DOUBLE, LLVMFAdd),
            });

            setName(MINUS, "-");
            binaryOperators.Add(MINUS, new[] {
                binary(MINUS, TypeTag.INT, TypeTag.INT, TypeTag.INT, LLVMSub),
                binary(MINUS, TypeTag.LONG, TypeTag.LONG, TypeTag.LONG, LLVMSub),
                binary(MINUS, TypeTag.FLOAT, TypeTag.FLOAT, TypeTag.FLOAT, LLVMFSub),
                binary(MINUS, TypeTag.DOUBLE, TypeTag.DOUBLE, TypeTag.DOUBLE, LLVMFSub),
            });

            setName(MUL, "*");
            binaryOperators.Add(MUL, new[] {
                binary(MUL, TypeTag.INT, TypeTag.INT, TypeTag.INT, LLVMMul),
                binary(MUL, TypeTag.LONG, TypeTag.LONG, TypeTag.LONG, LLVMMul),
                binary(MUL, TypeTag.FLOAT, TypeTag.FLOAT, TypeTag.FLOAT, LLVMFMul),
                binary(MUL, TypeTag.DOUBLE, TypeTag.DOUBLE, TypeTag.DOUBLE, LLVMFMul),
            });

            setName(DIV, "/");
            binaryOperators.Add(DIV, new[] {
                binary(DIV, TypeTag.INT, TypeTag.INT, TypeTag.INT, LLVMSDiv),
                binary(DIV, TypeTag.LONG, TypeTag.LONG, TypeTag.LONG, LLVMSDiv),
                binary(DIV, TypeTag.FLOAT, TypeTag.FLOAT, TypeTag.FLOAT, LLVMFDiv),
                binary(DIV, TypeTag.DOUBLE, TypeTag.DOUBLE, TypeTag.DOUBLE, LLVMFDiv),
            });

            setName(MOD, "%");
            binaryOperators.Add(MOD, new[] {
                binary(MOD, TypeTag.INT, TypeTag.INT, TypeTag.INT, LLVMSRem),
                binary(MOD, TypeTag.LONG, TypeTag.LONG, TypeTag.LONG, LLVMSRem),
                binary(MOD, TypeTag.FLOAT, TypeTag.FLOAT, TypeTag.FLOAT, LLVMFRem),
                binary(MOD, TypeTag.DOUBLE, TypeTag.DOUBLE, TypeTag.DOUBLE, LLVMFRem),
            });

            //            setName(BITOR_ASG, "|=");
            //            setName(BITXOR_ASG, "^=");
            //            setName(BITAND_ASG, "&=");
            //
            //            setName(SHL_ASG, "<<=");
            //            setName(SHR_ASG, ">>=");
            //            setName(PLUS_ASG, "+=");
            //            setName(MINUS_ASG, "-=");
            //            setName(MUL_ASG, "*=");
            //            setName(DIV_ASG, "/=");
            //            setName(MOD_ASG, "%=");
        }

        private OperatorSymbol unary(Tag tag, TypeTag argType, TypeTag result, LLVMOpcode opcode = default(LLVMOpcode))
        {
            MethodType methodType = new MethodType(
                CollectionUtils.singletonList<Type>(symtab.typeForTag(argType)),
                symtab.typeForTag(result)
            );
            return new OperatorSymbol(operatorNames[tag.operatorIndex()], symtab.noSymbol, methodType, opcode);
        }

        private OperatorSymbol binary(Tag tag, TypeTag left, TypeTag right, TypeTag result, LLVMOpcode opcode,
                                      LLVMRealPredicate realPredicate)
        {
            return binary(tag, left, right, result, opcode, (int)realPredicate);
        }

        private OperatorSymbol binary(Tag tag, TypeTag left, TypeTag right, TypeTag result, LLVMOpcode opcode,
                                      LLVMIntPredicate intPredicate)
        {
            return binary(tag, left, right, result, opcode, (int)intPredicate);
        }

        private OperatorSymbol binary(Tag tag, TypeTag left, TypeTag right, TypeTag result, LLVMOpcode opcode,
                                      int predicate = 0)
        {
            MethodType methodType = new MethodType(
                new Type[] {symtab.typeForTag(left), symtab.typeForTag(right)},
                symtab.typeForTag(result)
            );
            return new OperatorSymbol(operatorNames[tag.operatorIndex()], symtab.noSymbol, methodType, opcode,
                predicate);
        }

        private void setName(Tag tag, string name)
        {
            operatorNames[tag.operatorIndex()] = name;
        }
    }
}
