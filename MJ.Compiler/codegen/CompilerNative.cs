using System.Runtime.InteropServices;

using LLVMSharp;

namespace mj.compiler.codegen
{
    public static class CompilerNative
    {
        [DllImport("mj_compiler_native", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern void AddRewriteStatepointsForGCPass(LLVMPassManagerRef passManagerRef);
    }
}
