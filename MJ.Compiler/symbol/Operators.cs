using System.Collections.Generic;

using mj.compiler.main;
using mj.compiler.resources;
using mj.compiler.tree;
using mj.compiler.utils;

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
                unary(PRE_INC, TypeTag.INT, TypeTag.INT),
                unary(PRE_INC, TypeTag.LONG, TypeTag.LONG),
                unary(PRE_INC, TypeTag.FLOAT, TypeTag.DOUBLE),
                unary(PRE_INC, TypeTag.DOUBLE, TypeTag.DOUBLE)
            });

            setName(PRE_DEC, "--");
            unaryOperators.Add(PRE_DEC, new[] {
                unary(PRE_DEC, TypeTag.INT, TypeTag.INT),
                unary(PRE_DEC, TypeTag.LONG, TypeTag.LONG),
                unary(PRE_DEC, TypeTag.FLOAT, TypeTag.DOUBLE),
                unary(PRE_DEC, TypeTag.DOUBLE, TypeTag.DOUBLE)
            });

            setName(POST_INC, "++");
            unaryOperators.Add(POST_INC, new[] {
                unary(POST_INC, TypeTag.INT, TypeTag.INT),
                unary(POST_INC, TypeTag.LONG, TypeTag.LONG),
                unary(POST_INC, TypeTag.FLOAT, TypeTag.DOUBLE),
                unary(POST_INC, TypeTag.DOUBLE, TypeTag.DOUBLE)
            });

            setName(POST_DEC, "--");
            unaryOperators.Add(POST_DEC, new[] {
                unary(POST_DEC, TypeTag.INT, TypeTag.INT),
                unary(POST_DEC, TypeTag.LONG, TypeTag.LONG),
                unary(POST_DEC, TypeTag.FLOAT, TypeTag.DOUBLE),
                unary(POST_DEC, TypeTag.DOUBLE, TypeTag.DOUBLE)
            });

            setName(OR, "||");
            binaryOperators.Add(OR, new[] {
                binary(OR, TypeTag.BOOLEAN, TypeTag.BOOLEAN, TypeTag.BOOLEAN),
            });

            setName(AND, "&&");
            binaryOperators.Add(AND, new[] {
                binary(AND, TypeTag.BOOLEAN, TypeTag.BOOLEAN, TypeTag.BOOLEAN),
            });

            // Order of combination listing for binary operators matters for correct resolution
            // More assignable types must be listed after less assignable ones,
            // which is the order listed in the TypeTag enum.

            setName(BITOR, "|");
            binaryOperators.Add(BITOR, new[] {
                binary(BITOR, TypeTag.BOOLEAN, TypeTag.BOOLEAN, TypeTag.BOOLEAN),
                binary(BITOR, TypeTag.INT, TypeTag.INT, TypeTag.INT),
                binary(BITOR, TypeTag.LONG, TypeTag.LONG, TypeTag.LONG),
            });

            setName(BITXOR, "^");
            binaryOperators.Add(BITXOR, new[] {
                binary(BITXOR, TypeTag.BOOLEAN, TypeTag.BOOLEAN, TypeTag.BOOLEAN),
                binary(BITXOR, TypeTag.INT, TypeTag.INT, TypeTag.INT),
                binary(BITXOR, TypeTag.LONG, TypeTag.LONG, TypeTag.LONG),
            });

            setName(BITAND, "&");
            binaryOperators.Add(BITAND, new[] {
                binary(BITAND, TypeTag.BOOLEAN, TypeTag.BOOLEAN, TypeTag.BOOLEAN),
                binary(BITAND, TypeTag.INT, TypeTag.INT, TypeTag.INT),
                binary(BITAND, TypeTag.LONG, TypeTag.LONG, TypeTag.LONG),
            });

            setName(EQ, "==");
            binaryOperators.Add(EQ, new[] {
                binary(EQ, TypeTag.BOOLEAN, TypeTag.BOOLEAN, TypeTag.BOOLEAN),
                binary(EQ, TypeTag.INT, TypeTag.INT, TypeTag.BOOLEAN),
                binary(EQ, TypeTag.LONG, TypeTag.LONG, TypeTag.BOOLEAN),
                binary(EQ, TypeTag.FLOAT, TypeTag.FLOAT, TypeTag.BOOLEAN),
                binary(EQ, TypeTag.DOUBLE, TypeTag.DOUBLE, TypeTag.BOOLEAN),
            });

            setName(NEQ, "!=");
            binaryOperators.Add(NEQ, new[] {
                binary(NEQ, TypeTag.BOOLEAN, TypeTag.BOOLEAN, TypeTag.BOOLEAN),
                binary(NEQ, TypeTag.INT, TypeTag.INT, TypeTag.BOOLEAN),
                binary(NEQ, TypeTag.LONG, TypeTag.LONG, TypeTag.BOOLEAN),
                binary(NEQ, TypeTag.FLOAT, TypeTag.FLOAT, TypeTag.BOOLEAN),
                binary(NEQ, TypeTag.DOUBLE, TypeTag.DOUBLE, TypeTag.BOOLEAN),
            });

            setName(LT, "<");
            binaryOperators.Add(LT, new[] {
                binary(LT, TypeTag.INT, TypeTag.INT, TypeTag.BOOLEAN),
                binary(LT, TypeTag.LONG, TypeTag.LONG, TypeTag.BOOLEAN),
                binary(LT, TypeTag.FLOAT, TypeTag.FLOAT, TypeTag.BOOLEAN),
                binary(LT, TypeTag.DOUBLE, TypeTag.DOUBLE, TypeTag.BOOLEAN),
            });

            setName(GT, ">");
            binaryOperators.Add(GT, new[] {
                binary(GT, TypeTag.INT, TypeTag.INT, TypeTag.BOOLEAN),
                binary(GT, TypeTag.LONG, TypeTag.LONG, TypeTag.BOOLEAN),
                binary(GT, TypeTag.FLOAT, TypeTag.FLOAT, TypeTag.BOOLEAN),
                binary(GT, TypeTag.DOUBLE, TypeTag.DOUBLE, TypeTag.BOOLEAN),
            });

            setName(LE, "<=");
            binaryOperators.Add(LE, new[] {
                binary(LE, TypeTag.INT, TypeTag.INT, TypeTag.BOOLEAN),
                binary(LE, TypeTag.LONG, TypeTag.LONG, TypeTag.BOOLEAN),
                binary(LE, TypeTag.FLOAT, TypeTag.FLOAT, TypeTag.BOOLEAN),
                binary(LE, TypeTag.DOUBLE, TypeTag.DOUBLE, TypeTag.BOOLEAN),
            });

            setName(GE, ">=");
            binaryOperators.Add(GE, new[] {
                binary(GE, TypeTag.INT, TypeTag.INT, TypeTag.BOOLEAN),
                binary(GE, TypeTag.LONG, TypeTag.LONG, TypeTag.BOOLEAN),
                binary(GE, TypeTag.FLOAT, TypeTag.FLOAT, TypeTag.BOOLEAN),
                binary(GE, TypeTag.DOUBLE, TypeTag.DOUBLE, TypeTag.BOOLEAN),
            });

            setName(SHL, "<<");
            binaryOperators.Add(SHL, new[] {
                binary(SHL, TypeTag.INT, TypeTag.INT, TypeTag.INT),
                binary(SHL, TypeTag.INT, TypeTag.LONG, TypeTag.INT),
                binary(SHL, TypeTag.LONG, TypeTag.INT, TypeTag.LONG),
                binary(SHL, TypeTag.LONG, TypeTag.LONG, TypeTag.LONG),
            });

            setName(SHR, ">>");
            binaryOperators.Add(SHR, new[] {
                binary(SHR, TypeTag.INT, TypeTag.INT, TypeTag.INT),
                binary(SHR, TypeTag.INT, TypeTag.LONG, TypeTag.INT),
                binary(SHR, TypeTag.LONG, TypeTag.INT, TypeTag.LONG),
                binary(SHR, TypeTag.LONG, TypeTag.LONG, TypeTag.LONG),
            });

            setName(PLUS, "+");
            binaryOperators.Add(PLUS, new[] {
                binary(PLUS, TypeTag.INT, TypeTag.INT, TypeTag.INT),
                binary(PLUS, TypeTag.LONG, TypeTag.LONG, TypeTag.LONG),
                binary(PLUS, TypeTag.FLOAT, TypeTag.FLOAT, TypeTag.FLOAT),
                binary(PLUS, TypeTag.DOUBLE, TypeTag.DOUBLE, TypeTag.DOUBLE),
            });

            setName(MINUS, "-");
            binaryOperators.Add(MINUS, new[] {
                binary(MINUS, TypeTag.INT, TypeTag.INT, TypeTag.INT),
                binary(MINUS, TypeTag.LONG, TypeTag.LONG, TypeTag.LONG),
                binary(MINUS, TypeTag.FLOAT, TypeTag.FLOAT, TypeTag.FLOAT),
                binary(MINUS, TypeTag.DOUBLE, TypeTag.DOUBLE, TypeTag.DOUBLE),
            });

            setName(MUL, "*");
            binaryOperators.Add(MUL, new[] {
                binary(MUL, TypeTag.INT, TypeTag.INT, TypeTag.INT),
                binary(MUL, TypeTag.LONG, TypeTag.LONG, TypeTag.LONG),
                binary(MUL, TypeTag.FLOAT, TypeTag.FLOAT, TypeTag.FLOAT),
                binary(MUL, TypeTag.DOUBLE, TypeTag.DOUBLE, TypeTag.DOUBLE),
            });

            setName(DIV, "/");
            binaryOperators.Add(DIV, new[] {
                binary(DIV, TypeTag.INT, TypeTag.INT, TypeTag.INT),
                binary(DIV, TypeTag.LONG, TypeTag.LONG, TypeTag.LONG),
                binary(DIV, TypeTag.FLOAT, TypeTag.FLOAT, TypeTag.FLOAT),
                binary(DIV, TypeTag.DOUBLE, TypeTag.DOUBLE, TypeTag.DOUBLE),
            });

            setName(MOD, "%");
            binaryOperators.Add(MOD, new[] {
                binary(MOD, TypeTag.INT, TypeTag.INT, TypeTag.INT),
                binary(MOD, TypeTag.LONG, TypeTag.LONG, TypeTag.LONG),
                binary(MOD, TypeTag.FLOAT, TypeTag.FLOAT, TypeTag.FLOAT),
                binary(MOD, TypeTag.DOUBLE, TypeTag.DOUBLE, TypeTag.DOUBLE),
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

        private OperatorSymbol unary(Tag tag, TypeTag argType, TypeTag result)
        {
            MethodType methodType = new MethodType(
                CollectionUtils.singletonList<Type>(symtab.typeForTag(argType)),
                symtab.typeForTag(result)
            );
            return new OperatorSymbol(operatorNames[tag.operatorIndex()], symtab.noSymbol, methodType);
        }

        private OperatorSymbol binary(Tag tag, TypeTag left, TypeTag right, TypeTag result)
        {
            MethodType methodType = new MethodType(
                new Type[] {symtab.typeForTag(left), symtab.typeForTag(right)},
                symtab.typeForTag(result)
            );
            return new OperatorSymbol(operatorNames[tag.operatorIndex()], symtab.noSymbol, methodType);
        }

        private void setName(Tag tag, string name)
        {
            operatorNames[tag.operatorIndex()] = name;
        }
    }
}
