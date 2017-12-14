using System;

namespace mj.compiler.utils
{
    public static class Assert
    {
        public static T checkNonNull<T>(T val) where T : class
        {
            return val ?? throw new NullReferenceException();
        }

        public static void assert(bool value)
        {
            if (!value) {
                throw new InvalidOperationException();
            }
        }
    }
}
