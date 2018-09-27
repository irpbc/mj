using System;
using System.Collections.Generic;

using LLVMSharp;

using mj.compiler.utils;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace mj.compiler.symbol
{
    [JsonConverter(typeof(ToStringConverter))]
    public abstract class Type
    {
        public static readonly Type NO_TYPE = new NoType();

        [JsonConverter(typeof(StringEnumConverter))]
        public abstract TypeTag Tag { get; }

        public virtual bool IsPrimitive => false;
        public virtual bool IsNumeric => false;
        public virtual bool IsIntegral => false;
        public virtual bool IsBoolean => false;
        public virtual bool IsArray => false;
        public virtual bool IsRefType => false;
        public virtual bool IsVoid => false;
        public virtual bool IsError => false;

        /// <summary>
        /// The constant value of this type, null if this type does not
        /// have a constant value attribute. Only primitive types and strings
        /// can have a constant value attribute.
        /// </summary>
        /// <returns> the constant value attribute of this type </returns>
        public virtual Object ConstValue => null;

        /// Is this a constant type whose value is false?
        public virtual bool IsFalse => false;

        /// Is this a constant type whose value is true?
        public virtual bool IsTrue => false;

        public virtual IList<Type> ParameterTypes => CollectionUtils.emptyList<Type>();
        public virtual Type ReturnType => null;

        [JsonIgnore]
        public virtual Type BaseType => this;

        /// <summary>
        /// Subclasses must override to provide a string representation.
        /// </summary>
        /// <returns></returns>
        public abstract override string ToString();

        public abstract T accept<T>(TypeVisitor<T> v);
    }

    /// <summary>
    /// Class for built in types. Predefined in <see cref="Symtab"/>.
    /// </summary>
    public class PrimitiveType : Type
    {
        private readonly TypeTag tag;

        internal PrimitiveType(TypeTag tag)
        {
            this.tag = tag;
        }

        public override TypeTag Tag => tag;

        public override bool IsPrimitive => true;
        public override bool IsNumeric => tag.isNumeric();
        public override bool IsBoolean => tag == TypeTag.BOOLEAN;
        public override bool IsRefType => tag == TypeTag.STRING;
        public override bool IsIntegral => tag == TypeTag.INT || tag == TypeTag.LONG;
        public override bool IsVoid => tag == TypeTag.VOID;

        public override T accept<T>(TypeVisitor<T> v) => v.visitPrimitiveType(this);

        /** Define a constant type, of the same kind as this type
         *  and with given constant value
         */
        public ConstType constType(Object constValue)
        {
            Object value = constValue;
            return new ConstType(tag, value, this);
        }

        public class ConstType : PrimitiveType
        {
            public ConstType(TypeTag tag, Object value, PrimitiveType baseType) : base(tag)
            {
                this.ConstValue = value;
                this.BaseType = baseType;
            }

            public override Object ConstValue { get; }
            public override Type BaseType { get; }

            public override bool IsTrue => tag == TypeTag.BOOLEAN && (bool)ConstValue == true;
            public override bool IsFalse => tag == TypeTag.BOOLEAN && (bool)ConstValue == false;

            public override string ToString() => tag.asString() + " : " + ConstValue;
        }

        public override string ToString() => tag.asString();
    }

    public class StructType : Type
    {
        public readonly Symbol.StructSymbol symbol;

        public StructType(Symbol.StructSymbol symbol)
        {
            this.symbol = symbol;
        }

        public override TypeTag Tag => TypeTag.STRUCT;

        public override bool IsRefType => true;

        public override string ToString() => "Struct " + symbol.name;

        public override T accept<T>(TypeVisitor<T> v) => v.visitStructType(this);
    }

    public class ArrayType : Type
    {
        public Type elemType;
        public LLVMTypeRef llvmType;
        public LLVMValueRef llvmMetaRef;

        public ArrayType(Type elemType)
        {
            this.elemType = elemType;
        }

        public override TypeTag Tag => TypeTag.ARRAY;
        public override bool IsArray => true;
        public override bool IsRefType => true;

        public override string ToString() => elemType + "[]";

        public override T accept<T>(TypeVisitor<T> v) => v.visitArrayType(this);
    }

    /// <summary>
    /// Represents a func signature.
    /// </summary>
    public class FuncType : Type
    {
        public IList<Type> argTypes;
        public Type resType;
        public bool isVarArg;

        public FuncType(IList<Type> argTypes, Type resType, bool isVarArg = false)
        {
            this.argTypes = argTypes;
            this.resType = resType;
            this.isVarArg = isVarArg;
        }

        public override TypeTag Tag => TypeTag.FUNC;
        public override IList<Type> ParameterTypes => argTypes;
        public override Type ReturnType => resType;

        public override string ToString()
        {
            return resType + "(" + String.Join(", ", argTypes) + ")";
        }

        public override T accept<T>(TypeVisitor<T> v) => v.visitFuncType(this);
    }

    public sealed class NoType : Type
    {
        public override TypeTag Tag => TypeTag.NONE;
        public override bool IsError => true;

        public override string ToString() => "";

        public override T accept<T>(TypeVisitor<T> v) => throw new InvalidOperationException();
    }

    public sealed class ErrorType : Type
    {
        public override TypeTag Tag => TypeTag.ERROR;
        public override bool IsError => true;

        /// Return type of error is also error
        public override Type ReturnType => this;

        public override string ToString() => "<error>";

        public override T accept<T>(TypeVisitor<T> v) => throw new InvalidOperationException();
    }

    public class TypeVisitor<T>
    {
        public virtual T visitPrimitiveType(PrimitiveType prim) => visit(prim);
        public virtual T visitStructType(StructType structType) => visit(structType);
        public virtual T visitArrayType(ArrayType arrayType) => visit(arrayType);
        public virtual T visitFuncType(FuncType funcType) => visit(funcType);

        public virtual T visit(Type type) => throw new InvalidOperationException();
    }
}
