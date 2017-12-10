using System.Collections.Generic;

using mj.compiler.tree;
using mj.compiler.utils;

namespace mj.compiler.codegen
{
    public class CodeGenerator
    {
        private static readonly Context.Key<CodeGenerator> CONTEXT_KEY = new Context.Key<CodeGenerator>();

        public static CodeGenerator instance(Context ctx) =>
            ctx.tryGet(CONTEXT_KEY, out var instance) ? instance : new CodeGenerator(ctx);

        private CodeGenerator(Context ctx)
        {
            ctx.put(CONTEXT_KEY, this);
        }

        public void main(IEnumerable<CompilationUnit> trees)
        {
            
        }
    }
}
