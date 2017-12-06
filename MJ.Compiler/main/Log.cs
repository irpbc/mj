using System;

using mj.compiler.utils;

namespace mj.compiler.main
{
    public class Log
    {
        private static readonly Context.Key<Log> CONTEXT_KEY = new Context.Key<Log>();

        public static Log instance(Context ctx) =>
            ctx.tryGet(CONTEXT_KEY, out var instance) ? instance : new Log(ctx);

        private Log(Context ctx)
        {
            ctx.put(CONTEXT_KEY, this);
        }

        private const int MAX_ERRORS = 128;

        public int NumErrors { get; private set; } = 0;

        public void error(String format, params Object[] args)
        {
            ConsoleColor prevColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            try {
                Console.Write("Error:  ");
                Console.WriteLine(format, args);
            } finally {
                Console.ForegroundColor = prevColor;
            }

            NumErrors++;
        }
    }
}
