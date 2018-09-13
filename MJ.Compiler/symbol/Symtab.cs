using System;
using System.Collections.Generic;
using System.Net.Http.Headers;

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
        public readonly PrimitiveType stringType;
        public readonly PrimitiveType voidType;

        public readonly Type bottomType;

        public readonly Symbol.VarSymbol arrayLengthField;

        public readonly Type errorType;
        public readonly Symbol errorSymbol;

        public readonly Symbol.VarSymbol errorVarSymbol;
        public readonly Symbol.TypeSymbol errorTypeSymbol;

        public readonly Symbol.TypeSymbol noSymbol;

        //public Symbol.OperatorSymbol noOpSymbol;
        public Symbol.OperatorSymbol errorOpSymbol;

        public readonly Dictionary<Type, ArrayType> arrayTypes = new Dictionary<Type, ArrayType>();

        private sealed class NoSymbol : Symbol.TypeSymbol
        {
            public NoSymbol(Symbol owner, Type type) : base(Kind.MTH, "", owner, type) { }

            public override string ToString() => "<no symbol>";
        }

        private sealed class ErrorTypeSymbol : Symbol.TypeSymbol
        {
            public ErrorTypeSymbol(Symbol owner, Type type) : base(Kind.ERROR, "<error>", owner, type) { }

            public override string ToString() => "<error>";
        }
        
        private sealed class BottomType : Type
        {
            public BottomType() : base(null) { }

            public override TypeTag Tag => TypeTag.NULL;
            public override bool IsRefType => true;
            
            public override string ToString() => "null";
            
            public override T accept<T>(TypeVisitor<T> v) => default(T);
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
            stringType = primitive(TypeTag.STRING, "string");
            voidType = primitive(TypeTag.VOID, "void");
            
            bottomType = new BottomType();
            
            arrayLengthField = new Symbol.VarSymbol(Symbol.Kind.FIELD, "length", intType, noSymbol);
            arrayLengthField.fieldIndex = 0;

            errorSymbol = new Symbol.ErrorSymbol(topLevelSymbol, null);
            errorType = new ErrorType(errorSymbol);
            errorSymbol.type = errorType;
            
            errorTypeSymbol = new ErrorTypeSymbol(topLevelSymbol, errorType);

            errorVarSymbol = new Symbol.VarSymbol(Symbol.Kind.ERROR, "<error>", errorType, null);

            noSymbol = new NoSymbol(topLevelSymbol, Type.NO_TYPE);
            //noOpSymbol = new Symbol.OperatorSymbol("", noSymbol, Type.NO_TYPE);

            errorOpSymbol = new Symbol.OperatorSymbol("", noSymbol, errorType, 0);

            enterBuiltins();
        }

        public IList<Symbol.MethodSymbol> builtins;

        private PrimitiveType primitive(TypeTag typeTag, string name)
        {
            var sym = new Symbol.PrimitiveTypeSymbol(name, topLevelSymbol);
            var type = new PrimitiveType(typeTag, sym);
            sym.type = type;
            return type;
        }

        private void enterBuiltins()
        {
            Scope.WriteableScope scope = topLevelSymbol.topScope;
            builtins = new List<Symbol.MethodSymbol>();

            scope.enter(builtin("puts", intType, stringType));
            scope.enter(builtin("putchar", intType, intType));
            scope.enter(builtin("hello", voidType));
            scope.enter(builtinVararg("printf", intType, stringType));
            scope.enter(builtin("scan_int", intType));
            scope.enter(builtin("scan_long", longType));
            scope.enter(builtin("scan_float", floatType));
            scope.enter(builtin("scan_double", doubleType));
        }

        private Symbol builtin(string name, Type resType)
        {
            return builtin(name, resType, CollectionUtils.emptyList<Type>());
        }
        
        private Symbol builtinVararg(string name, Type resType, Type arg)
        {
            return builtin(name, resType, CollectionUtils.singletonList(arg), true);
        }

        private Symbol builtin(string name, Type resType, Type arg)
        {
            return builtin(name, resType, CollectionUtils.singletonList(arg));
        }

        private Symbol builtin(string name, Type resType, Type arg1, Type arg2)
        {
            return builtin(name, resType, new[] { arg1, arg2 });
        }

        private Symbol.MethodSymbol builtin(string name, Type resType, IList<Type> args, bool isVarArg = false)
        {
            Symbol.MethodSymbol ms = new Symbol.MethodSymbol(name, topLevelSymbol,
                new MethodType(args, resType, isVarArg));
            ms.isVararg = isVarArg;

            ms.parameters = new List<Symbol.VarSymbol>(args.Count);
            foreach (Type type in args) {
                ms.parameters.Add(new Symbol.VarSymbol(Symbol.Kind.PARAM, "arg", type, ms));
            }

            builtins.Add(ms);
            return ms;
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
                case TypeTag.STRING:
                    return stringType;
                case TypeTag.VOID:
                    return voidType;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tag), tag, null);
            }
        }

        public ArrayType arrayTypeForElemType(Type elemType)
        {
            if (arrayTypes.TryGetValue(elemType, out var arrayType)) {
                return arrayType;
            }
            arrayType = new ArrayType(elemType, elemType.definer);
            arrayTypes.Add(elemType, arrayType);
            return arrayType;
        }
    }
}
