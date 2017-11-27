using System;

using mj.compiler.utils;

namespace mj.compiler.main
{
    public static class Start
    {
        public static void Main(String[] args)
        {
            Context context = new Context();
            CommandLineOptions options = CommandLineOptions.instance(context);
            
            options.readOptions(args);
            
            Compiler compiler = Compiler.instance(context);
            compiler.compile();
        }
    }
}
