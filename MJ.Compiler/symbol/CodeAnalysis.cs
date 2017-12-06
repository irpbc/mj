using System.Collections.Generic;

using mj.compiler.parsing.ast;
using mj.compiler.utils;

namespace mj.compiler.symbol
{
    public class CodeAnalysis : AstVisitor<object>
    {
        private static readonly Context.Key<CodeAnalysis> CONTEXT_KEY = new Context.Key<CodeAnalysis>();

        public static CodeAnalysis instance(Context ctx) =>
            ctx.tryGet(CONTEXT_KEY, out var instance) ? instance : new CodeAnalysis(ctx);

        public CodeAnalysis(Context ctx)
        {
            ctx.put(CONTEXT_KEY, this);
        }

        public IEnumerable<CompilationUnit> main(IEnumerable<CompilationUnit> compilationUnits)
        {
            analyze(compilationUnits);
            return compilationUnits;
        }
        
        private void analyze<T>(IEnumerable<T> trees) where T : Tree
        {
            foreach (T tree in trees) {
                analyze(tree);
            }
        }

        private void analyze(Tree tree) => tree.accept(this);

        public override object visitCompilationUnit(CompilationUnit compilationUnit)
        {
            analyze(compilationUnit.methods);
            
            return null;
        }

        public override object visitMethodDef(MethodDef method)
        {
            return null;
        }
    }
}
