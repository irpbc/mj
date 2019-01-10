using System;
using System.Runtime.InteropServices;

using LLVMSharp;

namespace mj.compiler.codegen
{
    public static class LLVMUtils
    {
        public const int OBJECT_HEADER_FIELDS = 3;

        public static readonly LLVMTypeRef VOID = LLVM.VoidType();
        public static readonly LLVMTypeRef INT1 = LLVM.Int1Type();
        public static readonly LLVMTypeRef INT8 = LLVM.Int8Type();
        public static readonly LLVMTypeRef INT32 = LLVM.Int32Type();
        public static readonly LLVMTypeRef INT64 = LLVM.Int64Type();
        public static readonly LLVMTypeRef FLOAT = LLVM.FloatType();
        public static readonly LLVMTypeRef DOUBLE = LLVM.DoubleType();
        public static readonly LLVMTypeRef PTR_INT32 = PTR(INT32);
        public static readonly LLVMTypeRef PTR_INT8 = PTR(INT8);

        // LLVM does not accept a void pointer type
        public static readonly LLVMTypeRef PTR_VOID = PTR_INT8;

        public static readonly LLVMTypeRef META_TYPE = LLVM.StructType(new[] {
            INT8, // TypeKind
            INT8, // Array element size
            INT32, // Object size
            INT32, // Number of ref fields
            LLVM.ArrayType(INT32, 0) // Offsets of ref fields
        }, false);

        public static LLVMTypeRef FUNC_TYPE(LLVMTypeRef ret, params LLVMTypeRef[] args)
            => LLVM.FunctionType(ret, args, false);

        public static LLVMTypeRef PTR(LLVMTypeRef ty) => LLVM.PointerType(ty, 0);
        public static LLVMTypeRef HEAP_PTR(LLVMTypeRef ty) => LLVM.PointerType(ty, 1);
        public static LLVMValueRef NULL(LLVMTypeRef ptrTy) => LLVM.ConstNull(ptrTy);

        public static LLVMValueRef CONST_UINT8(int value) => LLVM.ConstInt(INT8, (ulong)value, false);
        public static LLVMValueRef CONST_INT8(int value) => LLVM.ConstInt(INT8, (ulong)value, true);
        public static LLVMValueRef CONST_INT32(int value) => LLVM.ConstInt(INT32, (ulong)value, true);
        public static LLVMValueRef CONST_INT64(long value) => LLVM.ConstInt(INT64, (ulong)value, true);
        public static LLVMValueRef CONST_FLOAT(float value) => LLVM.ConstReal(FLOAT, value);
        public static LLVMValueRef CONST_DOUBLE(double value) => LLVM.ConstReal(DOUBLE, value);

        public static LLVMValueRef SIZE_OF(LLVMTypeRef type)
        {
            return SIZE_OF(type, INT32);
        }
        
        public static LLVMValueRef SIZE_OF(LLVMTypeRef type, LLVMTypeRef resultType)
        {
            LLVMValueRef gep = LLVM.ConstInBoundsGEP(NULL(PTR(type)), new[] {CONST_INT32(1)});
            return LLVM.ConstPtrToInt(gep, resultType);
        }
        
        [StructLayout(LayoutKind.Explicit)]
        public struct BitCast
        {
            [FieldOffset(0)]
            public long longValue;

            [FieldOffset(0)]
            public ulong ulongValue;

            public BitCast(long l)
            {
                this.ulongValue = 0;
                this.longValue = l;
            }
        }
    }

    public static class LLVMModuleExtensions
    {
        public static LLVMValueRef Func(this LLVMModuleRef module, LLVMTypeRef ret, String name,
                                        params LLVMTypeRef[] args)
        {
            return LLVM.AddFunction(module, name, LLVMUtils.FUNC_TYPE(ret, args));
        }
    }
}
