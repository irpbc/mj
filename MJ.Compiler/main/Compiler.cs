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
        private readonly CodeAnalysis codeAnalysis;
        private readonly CodeGenerator codeGenerator;

        private readonly Log log;

        private Compiler(Context context)
        {
            context.put(CONTEX_KEY, this);
            options = CommandLineOptions.instance(context);
            parser = ParserRunner.instance(context);
            declarationAnalysis = DeclarationAnalysis.instance(context);
            codeAnalysis = CodeAnalysis.instance(context);
            codeGenerator = CodeGenerator.instance(context);
            log = Log.instance(context);
        }

        public void compile()
        {
            List<SourceFile> files = options.InputFiles.Select(s => new SourceFile(s)).ToList();

            generateCode(analyseCode(analyseDecls(parse(files))));
        }

        private IList<CompilationUnit> parse(IList<SourceFile> files)
        {
            IList<CompilationUnit> results = new CompilationUnit[files.Count];
            for (var i = 0; i < files.Count; i++) {
                SourceFile sourceFile = files[i];
                CompilationUnit compilationUnit = parser.parse(sourceFile);
                compilationUnit.sourceFile = sourceFile;
                results[i] = compilationUnit;
            }
            return results;
        }

        private IList<CompilationUnit> analyseDecls(IList<CompilationUnit> trees)
        {
            return stopIfError(declarationAnalysis.main(trees));
        }

        private IList<CompilationUnit> analyseCode(IList<CompilationUnit> trees)
        {
            IList<CompilationUnit> compilationUnits = stopIfError(codeAnalysis.main(trees));
            if (options.DumpTree) {
                String dump = JsonConvert.SerializeObject(compilationUnits, Formatting.Indented);
                Console.WriteLine(dump);
            }
            return compilationUnits;
        }

        private void generateCode(IEnumerable<CompilationUnit> trees)
        {
            codeGenerator.main(trees);
        }

        private IList<CompilationUnit> stopIfError(IList<CompilationUnit> trees)
        {
            if (log.NumErrors > 0) {
                return CollectionUtils.emptyList<CompilationUnit>();
            }
            return trees;
        }
    }
}
