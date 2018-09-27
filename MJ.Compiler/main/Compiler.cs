using System;
using System.Collections.Generic;
using System.Linq;

using mj.compiler.codegen;
using mj.compiler.parsing;
using mj.compiler.symbol;
using mj.compiler.tree;
using mj.compiler.utils;

using Newtonsoft.Json;

namespace mj.compiler.main
{
    public class Compiler
    {
        private static readonly Context.Key<Compiler> CONTEX_KEY = new Context.Key<Compiler>();

        public static Compiler instance(Context context) =>
            context.tryGet(CONTEX_KEY, out var instance) ? instance : new Compiler(context);

        private readonly CommandLineOptions options;
        private readonly ParserRunner parser;
        private readonly DeclarationAnalysis declarationAnalysis;
        private readonly TypeAnalysis typeAnalysis;
        private readonly FlowAnalysis flowAnalysis;
        private readonly CodeGeneration codeGeneration;

        private readonly Log log;

        private Compiler(Context context)
        {
            context.put(CONTEX_KEY, this);
            options = CommandLineOptions.instance(context);
            parser = ParserRunner.instance(context);
            declarationAnalysis = DeclarationAnalysis.instance(context);
            typeAnalysis = TypeAnalysis.instance(context);
            flowAnalysis = FlowAnalysis.instance(context);
            codeGeneration = CodeGeneration.instance(context);
            log = Log.instance(context);
        }

        public void compile()
        {
            IList<string> inputs = options.InputFiles;
            if (inputs.Count == 0) {
                return;
            }
            List<SourceFile> files = inputs.Select(s => new SourceFile(s)).ToList();

            generateCode(flow(types(decls(parse(files)))));
        }

        private IList<CompilationUnit> parse(IList<SourceFile> files)
        {
            IList<CompilationUnit> results = new CompilationUnit[files.Count];
            for (var i = 0; i < files.Count; i++) {
                SourceFile sourceFile = files[i];
                CompilationUnit compilationUnit = parser.parse(sourceFile);
                results[i] = compilationUnit;
            }
            return stopIfError(results);
        }

        private IList<CompilationUnit> decls(IList<CompilationUnit> trees) =>
            stopIfError(declarationAnalysis.main(trees));

        private IList<CompilationUnit> types(IList<CompilationUnit> trees)
        {
            IList<CompilationUnit> compilationUnits = stopIfError(typeAnalysis.main(trees));
            if (options.DumpTree) {
                String dump = JsonConvert.SerializeObject(compilationUnits, Formatting.Indented);
                Console.WriteLine(dump);
            }
            return compilationUnits;
        }

        private IList<CompilationUnit> flow(IList<CompilationUnit> trees) => stopIfError(flowAnalysis.main(trees));

        private void generateCode(IList<CompilationUnit> trees) => codeGeneration.main(trees);

        private IList<CompilationUnit> stopIfError(IList<CompilationUnit> trees)
        {
            if (log.NumErrors > 0) {
                return CollectionUtils.emptyList<CompilationUnit>();
            }
            return trees;
        }
    }
}
