using System;
using System.Collections.Generic;

using LLVMSharp;

using mj.compiler.codegen;
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

        public class StructSymbol : TypeSymbol
        {
            public Scope.WriteableScope membersScope;
            public LLVMTypeRef llvmTypeRef;
            public LLVMValueRef llvmMetaRef;

            public StructSymbol(string name, Symbol owner, Type type) 
                : base(Kind.STRUCT, name, owner, type) { }

            public override string ToString() => "Struct " + name;
        }

        public class FuncSymbol : Symbol
        {
            public IList<VarSymbol> parameters;
            public Scope.WriteableScope scope;
            public LLVMValueRef llvmRef;
            public bool isVararg;
            public bool isInvoked;

            public FuncSymbol(string name, Symbol owner, Type type) : base(Kind.FUNC, name, owner, type) { }

            public override string ToString() => name + ": " + type;
        }

        public class VarSymbol : Symbol
        {
            public LLVMValueRef llvmRef;
            public int fieldIndex;

            public VarSymbol(Kind kind, string name, Type type, Symbol owner) : base(kind, name, owner, type) { }

            public override string ToString() => name + ": " + type;

            public int LLVMFieldIndex => fieldIndex + LLVMUtils.OBJECT_HEADER_FIELDS;
        }

        public abstract class TypeSymbol : Symbol
        {
            protected TypeSymbol(Kind kind, string name, Symbol owner, Type type) : base(kind, name, owner, type) { }
        }

        public class OperatorSymbol : FuncSymbol
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
            STRUCT = TOP << 1,
            FUNC = STRUCT << 1,
            PARAM = FUNC << 1,
            LOCAL = PARAM << 1,
            FIELD = LOCAL << 1,
            PRIMITIVE = FIELD << 1,
            OP = PRIMITIVE << 1,
            ERROR = OP << 1,

            VAR = PARAM | LOCAL | FIELD,
        }
    }
}
