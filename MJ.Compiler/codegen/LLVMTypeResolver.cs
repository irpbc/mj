using System;

using LLVMSharp;

using mj.compiler.symbol;

namespace mj.compiler.codegen
{
    public class LLVMTypeResolver : TypeVisitor<LLVMTypeRef>
    {
        public override LLVMTypeRef visitPrimitiveType(PrimitiveType prim)
        {
            switch (prim.Tag) {
                case TypeTag.INT: return LLVMTypeRef.Int32Type();
                case TypeTag.LONG: return LLVMTypeRef.Int64Type();
                case TypeTag.FLOAT: return LLVMTypeRef.FloatType();
                case TypeTag.DOUBLE: return LLVMTypeRef.DoubleType();
                case TypeTag.BOOLEAN: return LLVMTypeRef.Int1Type();
                case TypeTag.VOID: return LLVMTypeRef.VoidType();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override LLVMTypeRef visitMethodType(MethodType methodType)
        {
            LLVMTypeRef retType = methodType.ReturnType.accept(this);
            LLVMTypeRef[] paramTypes = new LLVMTypeRef[methodType.ParameterTypes.Count];
            for (var i = 0; i < paramTypes.Length; i++) {
                paramTypes[i] = methodType.ParameterTypes[i].accept(this);
            }
            return LLVMTypeRef.FunctionType(retType, paramTypes, false);
        }
    }
}
