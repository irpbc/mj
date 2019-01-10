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
                case TypeTag.CHAR: return INT8;
                case TypeTag.C_STRING: return PTR_INT8;
                case TypeTag.VOID: return VOID;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override LLVMTypeRef visitFuncType(FuncType funcType)
        {
            LLVMTypeRef retType = funcType.ReturnType.accept(this);

            int    parameterTypesCount = funcType.ParameterTypes.Count;
            Symbol funcOwner           = funcType.symbol.owner;
            bool   isMember            = funcOwner.kind == Symbol.Kind.STRUCT;

            if (isMember) {
                parameterTypesCount++;
            }
            LLVMTypeRef[] paramTypes = new LLVMTypeRef[parameterTypesCount];
            if (isMember) {
                paramTypes[0] = HEAP_PTR(((Symbol.StructSymbol)funcOwner).llvmTypeRef);
                for (int i = 1; i < paramTypes.Length; i++) {
                    paramTypes[i] = funcType.ParameterTypes[i - 1].accept(this);
                }
            } else {
                for (int i = 0; i < paramTypes.Length; i++) {
                    paramTypes[i] = funcType.ParameterTypes[i].accept(this);
                }
            }
            return LLVM.FunctionType(retType, paramTypes, funcType.isVarArg);
        }

        public override LLVMTypeRef visitStructType(StructType structType)
        {
            return HEAP_PTR(structType.symbol.llvmTypeRef);
        }

        public override LLVMTypeRef visitArrayType(ArrayType arrayType)
        {
            return HEAP_PTR(arrayType.llvmType);
        }
    }
}
