using System;

using LLVMSharp;

using mj.compiler.symbol;

using Type = mj.compiler.symbol.Type;

namespace mj.compiler.codegen
{
    public class LLVMTypeResolver : TypeVisitor<LLVMTypeRef>
    {
        public LLVMTypeRef resolve(Type type) => type.accept(this);

        public override LLVMTypeRef visitPrimitiveType(PrimitiveType prim)
        {
            switch (prim.Tag) {
                case TypeTag.INT: return LLVMTypeRef.Int32Type();
                case TypeTag.LONG: return LLVMTypeRef.Int64Type();
                case TypeTag.FLOAT: return LLVMTypeRef.FloatType();
                case TypeTag.DOUBLE: return LLVMTypeRef.DoubleType();
                case TypeTag.BOOLEAN: return LLVMTypeRef.Int1Type();
                case TypeTag.STRING: return LLVMTypeRef.PointerType(LLVMTypeRef.Int8Type(), 0);
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
            return LLVMTypeRef.FunctionType(retType, paramTypes, methodType.isVarArg);
        }

        public override LLVMTypeRef visitClassType(ClassType classType)
        {
            return LLVM.PointerType(((Symbol.ClassSymbol)classType.definer).llvmPointer, 0);
        }
    }
}
