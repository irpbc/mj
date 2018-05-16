using System;

using LLVMSharp;

using mj.compiler.symbol;

using static mj.compiler.codegen.LLVMUtils;

using Type = mj.compiler.symbol.Type;

namespace mj.compiler.codegen
{
    public class LLVMTypeResolver : TypeVisitor<LLVMTypeRef>
    {
        public LLVMTypeRef resolve(Type type) => type.accept(this);

        public override LLVMTypeRef visitPrimitiveType(PrimitiveType prim)
        {
            switch (prim.Tag) {
                case TypeTag.INT: return INT32;
                case TypeTag.LONG: return INT64;
                case TypeTag.FLOAT: return FLOAT;
                case TypeTag.DOUBLE: return DOUBLE;
                case TypeTag.BOOLEAN: return INT1;
                case TypeTag.STRING: return PTR_INT8;
                case TypeTag.VOID: return VOID;
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
            return LLVM.FunctionType(retType, paramTypes, methodType.isVarArg);
        }

        public override LLVMTypeRef visitClassType(ClassType classType)
        {
            return HEAP_PTR(((Symbol.ClassSymbol)classType.definer).llvmTypeRef);
        }

        public override LLVMTypeRef visitArrayType(ArrayType arrayType)
        {
            return HEAP_PTR(arrayType.llvmType);
        }
    }
}
