using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace mj.compiler.symbol
{
    public abstract class Symbol
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public Kind kind;

        public String name;

        /// <summary>
        /// Eg. 
        /// </summary>
        [JsonIgnore]
        public Symbol owner;

        /// <summary>
        /// Type of this symbol. 
        /// </summary>
        public Type type;

        protected Symbol(Kind kind, string name, Symbol owner, Type type)
        {
            this.kind = kind;
            this.name = name;
            this.owner = owner;
            this.type = type;
        }

        public class TopLevelSymbol : Symbol
        {
            public Scope.WriteableScope topScope;

            public TopLevelSymbol() : base(Kind.TOP, null, null, null) { }
        }

        public class MethodSymbol : Symbol
        {
            public IList<VarSymbol> parameters;
            public Scope.WriteableScope scope;

            public MethodSymbol(string name, Symbol owner, Type type) : base(Kind.MTH, name, owner, type) { }
        }

        public class VarSymbol : Symbol
        {
            public VarSymbol(Kind kind, string name, Type type, Symbol owner) : base(kind, name, owner, type) { }
        }

        public abstract class TypeSymbol : Symbol
        {
            protected TypeSymbol(Kind kind, string name, Symbol owner, Type type) : base(kind, name, owner, type) { }
        }

        public class PrimitiveTypeSymbol : TypeSymbol
        {
            public PrimitiveTypeSymbol(string name, Symbol owner) : base(Kind.PRIMITIVE, name, owner, null) { }
        }

        public class OperatorSymbol : Symbol
        {
            public OperatorSymbol(string name, Symbol owner, Type type) : base(Kind.OP, name, owner, type) { }
        }

        public sealed class ErrorSymbol : TypeSymbol
        {
            public ErrorSymbol(Symbol owner, Type type)
                : base(Kind.ERROR, "", owner, type) { }
        }

        [Flags]
        public enum Kind
        {
            TOP = 1,
            MTH = TOP << 1,
            PARAM = MTH << 1,
            LOCAL = PARAM << 1,
            PRIMITIVE = LOCAL << 1,
            OP = PRIMITIVE << 1,
            ERROR = OP << 1,

            VAR = PARAM | LOCAL,
        }
    }

    public static class SymbolKindExtensions
    {
        public static bool hasAny(this Symbol.Kind kind, Symbol.Kind test)
        {
            return (kind & test) != 0;
        }
    }
}
