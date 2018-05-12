using System.Runtime.InteropServices;

using LLVMSharp;

namespace mj.compiler.codegen
{
    public static class CompilerNative
    {
        [DllImport("libLLVM", EntryPoint = "AddRewriteStatepointsForGCPass",
            CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private static extern void AddRewriteStatepointsForGCPass_Win(LLVMPassManagerRef passManagerRef);

        [DllImport("mj_compiler_native", EntryPoint = "AddRewriteStatepointsForGCPass",
            CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private static extern void AddRewriteStatepointsForGCPass_Nix(LLVMPassManagerRef passManagerRef);

        public static void AddRewriteStatepointsForGCPass(LLVMPassManagerRef passManagerRef)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                AddRewriteStatepointsForGCPass_Win(passManagerRef);
            } else {
                AddRewriteStatepointsForGCPass_Nix(passManagerRef);
            }
        }
    }
}
