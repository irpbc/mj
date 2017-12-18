using System;
using System.IO;

using Antlr4.Runtime;

using mj.compiler.main;
using mj.compiler.tree;
using mj.compiler.utils;

namespace mj.compiler.parsing
{
    public class ParserRunner
    {
        private static readonly Context.Key<ParserRunner> CONTEX_KEY = new Context.Key<ParserRunner>();

        public static ParserRunner instance(Context context) => 
            context.tryGet(CONTEX_KEY, out var instance) ? instance : new ParserRunner(context);

        private CommandLineOptions options;
        private Log log;

        private ParserRunner(Context context)
        {
            context.put(CONTEX_KEY, this);
            options = CommandLineOptions.instance(context);
            log = Log.instance(context);
        }

        public CompilationUnit parse(SourceFile sourceFile)
        {
            using (Stream inStream = sourceFile.openInput()) {
                AntlrInputStream antlrInputStream = new AntlrInputStream(inStream);
                MJLexer lexer = new MJLexer(antlrInputStream);
                MJParser parser = new MJParser(new BufferedTokenStream(lexer));

                parser.ErrorHandler = new DefaultErrorStrategy();
                parser.AddErrorListener(new DiagnosticErrorListener());

                MJParser.CompilationUnitContext compilationUnit = parser.compilationUnit();
                
                if (parser.NumberOfSyntaxErrors > 0) {
                    Console.WriteLine("SYNTAX ERRORS");
                }
                
                return (CompilationUnit)compilationUnit.Accept(new AstGeneratingParseTreeVisitor(log));
            }
        }
    }
}
