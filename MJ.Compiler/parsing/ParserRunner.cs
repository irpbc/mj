using System;
using System.IO;

using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Misc;

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
                parser.AddErrorListener(ThrowingErrorListener.INSTANCE);

                MJParser.CompilationUnitContext compilationUnit = parser.compilationUnit();

                if (parser.NumberOfSyntaxErrors > 0) {
                    Console.WriteLine("SYNTAX ERRORS");
                    return null;
                }

                return (CompilationUnit) compilationUnit.Accept(new AstGeneratingParseTreeVisitor(log));
            }
        }
    }

    public class ThrowingErrorListener : BaseErrorListener
    {
        public static readonly ThrowingErrorListener INSTANCE = new ThrowingErrorListener();

        public override void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line,
            int charPositionInLine, string msg, RecognitionException e)
        {
            throw new ParseCanceledException("line " + line + ":" + charPositionInLine + " " + msg);
        }
    }
}
