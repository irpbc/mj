using System;
using System.Collections.Generic;
using System.Linq;

using mj.compiler.parsing;
using mj.compiler.parsing.ast;
using mj.compiler.utils;

using Newtonsoft.Json;

namespace mj.compiler.main
{
    public class Compiler
    {
        private static readonly Context.Key<Compiler> CONTEX_KEY = new Context.Key<Compiler>();

        public static Compiler instance(Context context)
        {
            if (context.tryGet(CONTEX_KEY, out var instance)) {
                return instance;
            }
            return new Compiler(context);
        }

        private readonly Context context;
        private readonly CommandLineOptions options;
        private readonly ParserRunner parser;

        private Compiler(Context context)
        {
            context.put(CONTEX_KEY, this);
            this.context = context;
            options = CommandLineOptions.instance(context);
            parser = ParserRunner.instance(context);
        }

        public void compile()
        {
            List<SourceFile> files = options.InputFiles.Select(s => new SourceFile(s)).ToList();
            IList<CompilatioUnit> compilatioUnits = parse(files);

            if (options.DumpTree) {
                String dump = JsonConvert.SerializeObject(compilatioUnits, Formatting.Indented);
                Console.WriteLine(dump);
            }
        }

        private IList<CompilatioUnit> parse(IList<SourceFile> files)
        {
            IList<CompilatioUnit> results = new CompilatioUnit[files.Count];
            for (var i = 0; i < files.Count; i++) {
                results[i] = parser.parse(files[i]);
            }
            return results;
        }
    }
}
