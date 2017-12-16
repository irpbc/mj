using System;

using mj.compiler.utils;

namespace mj.compiler.symbol
{
    /// <summary>
    /// Contains prefefined symbols
    /// </summary>
    public class Symtab
    {
        private static readonly Context.Key<Symtab> CONTEXT_KEY = new Context.Key<Symtab>();

        public static Symtab instance(Context context) =>
            context.tryGet(CONTEXT_KEY, out var instance) ? instance : new Symtab(context);

        public readonly Symbol.TopLevelSymbol topLevelSymbol;

        public readonly PrimitiveType intType;
        public readonly PrimitiveType longType;
        public readonly PrimitiveType floatType;
        public readonly PrimitiveType doubleType;
        public readonly PrimitiveType booleanType;
        public readonly PrimitiveType voidType;

        public readonly Type errorType;
        public readonly Symbol errorSymbol;
        
        public readonly Symbol.TypeSymbol noSymbol;
        //public Symbol.OperatorSymbol noOpSymbol;
        public Symbol.OperatorSymbol errorOpSymbol;

        private sealed class NoSymbol : Symbol.TypeSymbol
        {
            public NoSymbol(Symbol owner, Type type) : base(Kind.MTH, "", owner, type) { }

            public override string ToString() => "<no symbol>";
        }

        private Symtab(Context context)
        {
            context.put(CONTEXT_KEY, this);

            topLevelSymbol = new Symbol.TopLevelSymbol();
            topLevelSymbol.topScope = Scope.WriteableScope.create(topLevelSymbol);

            intType = primitive(TypeTag.INT, "int");
            longType = primitive(TypeTag.LONG, "long");
            floatType = primitive(TypeTag.FLOAT, "float");
            doubleType = primitive(TypeTag.DOUBLE, "double");
            booleanType = primitive(TypeTag.BOOLEAN, "boolean");
            voidType = primitive(TypeTag.VOID, "void");
            
            errorSymbol = new Symbol.ErrorSymbol(topLevelSymbol, null);
            errorType = new ErrorType(errorSymbol);
            errorSymbol.type = errorType;
            
            noSymbol = new NoSymbol(topLevelSymbol, Type.NO_TYPE);
            //noOpSymbol = new Symbol.OperatorSymbol("", noSymbol, Type.NO_TYPE);

            errorOpSymbol = new Symbol.OperatorSymbol("", noSymbol, errorType, 0);
        }

        private PrimitiveType primitive(TypeTag typeTag, string name)
        {
            var sym = new Symbol.PrimitiveTypeSymbol(name, topLevelSymbol);
            var type = new PrimitiveType(typeTag, sym);
            sym.type = type;
            return type;
        }

        public PrimitiveType typeForTag(TypeTag tag)
        {
            switch (tag) {
                case TypeTag.INT:
                    return intType;
                case TypeTag.LONG:
                    return longType;
                case TypeTag.FLOAT:
                    return floatType;
                case TypeTag.DOUBLE:
                    return doubleType;
                case TypeTag.BOOLEAN:
                    return booleanType;
                case TypeTag.VOID:
                    return voidType;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tag), tag, null);
            }
        }
    }
}
