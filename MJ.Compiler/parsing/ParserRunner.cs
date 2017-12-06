using System;
using System.IO;

using Antlr4.Runtime;

using mj.compiler.main;
using mj.compiler.parsing.ast;
using mj.compiler.utils;

namespace mj.compiler.parsing
{
    public class ParserRunner
    {
        private static readonly Context.Key<ParserRunner> CONTEX_KEY = new Context.Key<ParserRunner>();

        public static ParserRunner instance(Context context)
        {
            if (context.tryGet(CONTEX_KEY, out var instance)) {
                return instance;
            }
            return new ParserRunner(context);
        }

        private CommandLineOptions options;

        private ParserRunner(Context context)
        {
            context.put(CONTEX_KEY, this);
            options = CommandLineOptions.instance(context);
        }

        public CompilationUnit parse(SourceFile sourceFile)
        {
            using (Stream inStream = sourceFile.openInput()) {
                AntlrInputStream antlrInputStream = new AntlrInputStream(inStream);
                MJLexer lexer = new MJLexer(antlrInputStream);
                MJParser parser = new MJParser(new BufferedTokenStream(lexer));
                MJParser.CompilationUnitContext compilationUnit = parser.compilationUnit();

                return (CompilationUnit)compilationUnit.Accept(new AstGeneratingParseTreeVisitor());
            }
        }
    }
}
