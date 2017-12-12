using System;
using System.Collections.Generic;
using System.Threading;

using mj.compiler.utils;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace mj.compiler.symbol
{
    [JsonConverter(typeof(ToStringConverter))]
    public abstract class Type
    {
        public static readonly Type NO_TYPE = new NoType();

        /// Symbol that defines this type. 
        /// (eg. <see cref="Symbol.PrimitiveTypeSymbol"/> for <see cref="PrimitiveType"/>)
        [JsonIgnore]
        public Symbol definer;

        protected Type(Symbol definer)
        {
            this.definer = definer;
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public abstract TypeTag Tag { get; }

        public virtual bool IsNumeric => false;
        public virtual bool IsIntegral => false;
        public virtual bool IsBoolean => false;
        public virtual bool IsVoid => false;
        public virtual bool IsError => false;

        /// <summary>
        /// The constant value of this type, null if this type does not
        /// have a constant value attribute. Only primitive types and
        /// strings (ClassType) can have a constant value attribute.
        /// </summary>
        /// <returns> the constant value attribute of this type </returns>
        public virtual Object ConstValue => null;

        /// Is this a constant type whose value is false?
        public virtual bool IsFalse => false;

        /// Is this a constant type whose value is true?
        public virtual bool IsTrue => false;

        public virtual String StringValue => null;

        public virtual IList<Type> ParameterTypes => CollectionUtils.emptyList<Type>();
        public virtual Type ReturnType => null;

        [JsonIgnore]
        public virtual Type BaseType => this;

        /// <summary>
        /// Subclasses must override to provide a string representation.
        /// </summary>
        /// <returns></returns>
        public abstract override string ToString();
    }

    /// <summary>
    /// Class for built in types. Predefined in <see cref="Symtab"/>.
    /// </summary>
    public class PrimitiveType : Type
    {
        private readonly TypeTag tag;

        internal PrimitiveType(TypeTag tag, Symbol definer) : base(definer)
        {
            this.tag = tag;
        }

        public override bool IsNumeric => tag.isNumeric();
        public override bool IsBoolean => tag == TypeTag.BOOLEAN;

        public override bool IsIntegral {
            get {
                switch (tag) {
                    case TypeTag.INT:
                    case TypeTag.LONG:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public override TypeTag Tag => tag;

        public override bool IsVoid => tag == TypeTag.VOID;

        /** Define a constant type, of the same kind as this type
         *  and with given constant value
         */
        public Type constType(Object constValue)
        {
            Object value = constValue;
            return new ConstType(tag, value, this);
        }

        private class ConstType : PrimitiveType
        {
            public ConstType(TypeTag tag, Object value, PrimitiveType baseType) : base(tag, baseType.definer)
            {
                this.ConstValue = value;
                this.BaseType = baseType;
            }

            public override Object ConstValue { get; }
            public override Type BaseType { get; }

            public override bool IsTrue => tag == TypeTag.BOOLEAN && (bool)ConstValue == true;
            public override bool IsFalse => tag == TypeTag.BOOLEAN && (bool)ConstValue == false;
        }

        /// The constant value of this type, converted to String
        public override String StringValue => ConstValue?.ToString();
 
        public override string ToString()
        {
            return tag.asString();
        }
    }

    /// <summary>
    /// Represents a method signature.
    /// </summary>
    public class MethodType : Type
    {
        public IList<Type> argTypes;
        public Type resType;

        public MethodType(IList<Type> argTypes, Type resType)
            : base(null)
        {
            this.argTypes = argTypes;
            this.resType = resType;
        }

        public override TypeTag Tag => TypeTag.METHOD;
        public override IList<Type> ParameterTypes => argTypes;
        public override Type ReturnType => resType;

        public override string ToString()
        {
            return resType + "(" + String.Join(", ", argTypes) + ")";
        }
    }

    public sealed class NoType : Type
    {
        public NoType() : base(null) { }
        public override TypeTag Tag => TypeTag.NONE;
        public override bool IsError => true;

        public override string ToString() => "";
    }

    public sealed class ErrorType : Type
    {
        public ErrorType(Symbol definer) : base(definer) { }

        public override TypeTag Tag => TypeTag.ERROR;
        public override bool IsError => true;

        /// Return type of error is also error
        public override Type ReturnType => this;

        public override string ToString() => "<error>";
    }
}
