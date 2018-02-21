using System;
using System.Collections.Generic;

using LLVMSharp;

using mj.compiler.utils;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace mj.compiler.symbol
{
    [JsonConverter(typeof(ToStringConverter))]
    public abstract class Symbol
    {
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

        public abstract override string ToString();

        public class TopLevelSymbol : Symbol
        {
            public Scope.WriteableScope topScope;

            public TopLevelSymbol() : base(Kind.TOP, null, null, null) { }

            public override string ToString() => "<top level>";
        }

        public class MethodSymbol : Symbol
        {
            public IList<VarSymbol> parameters;
            public Scope.WriteableScope scope;
            public LLVMValueRef llvmPointer;
            public bool isVararg;
            public bool isInvoked;

            public MethodSymbol(string name, Symbol owner, Type type) : base(Kind.MTH, name, owner, type) { }

            public override string ToString() => name + ": " + type;
        }
        
        public class AspectSymbol : Symbol
        {
            public MethodSymbol afterMethod;
            
            public AspectSymbol(string name, Symbol owner) : base(Kind.ASPECT, name, owner, null)
            {
                this.name = name;
            }

            public override string ToString() => "aspect " + name;
        }

        public class VarSymbol : Symbol
        {
            public LLVMValueRef llvmPointer;

            public VarSymbol(Kind kind, string name, Type type, Symbol owner) : base(kind, name, owner, type) { }

            public override string ToString() => name + ": " + type;
        }

        public abstract class TypeSymbol : Symbol
        {
            protected TypeSymbol(Kind kind, string name, Symbol owner, Type type) : base(kind, name, owner, type) { }
        }

        public class PrimitiveTypeSymbol : TypeSymbol
        {
            public PrimitiveTypeSymbol(string name, Symbol owner) : base(Kind.PRIMITIVE, name, owner, null) { }

            public override string ToString() => name;
        }

        public class OperatorSymbol : MethodSymbol
        {
            public LLVMOpcode opcode;
            public int llvmPredicate;

            public OperatorSymbol(string name, Symbol owner, Type type, LLVMOpcode opcode) : base(name, owner, type)
            {
                this.opcode = opcode;
            }

            public OperatorSymbol(string name, Symbol owner, Type type, LLVMOpcode opcode,
                                  int predicate) 
                : this(name, owner, type, opcode)
            {
                llvmPredicate = predicate;
            }

            public bool IsComparison => type.ReturnType.IsBoolean && type.ParameterTypes[0].IsNumeric;
        }

        public sealed class ErrorSymbol : TypeSymbol
        {
            public ErrorSymbol(Symbol owner, Type type)
                : base(Kind.ERROR, "", owner, type) { }

            public override string ToString() => "<error>";
        }

        [Flags]
        [JsonConverter(typeof(StringEnumConverter))]
        public enum Kind
        {
            TOP = 1,
            MTH = TOP << 1,
            ASPECT = MTH << 1,
            PARAM = ASPECT << 1,
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
