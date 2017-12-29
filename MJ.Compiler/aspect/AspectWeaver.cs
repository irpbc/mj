using System;
using System.Collections;
using System.Collections.Generic;

using mj.compiler.symbol;
using mj.compiler.tree;
using mj.compiler.utils;

namespace mj.compiler.aspect
{
    public class AspectWeaver : AstVisitor<object, AspectWeaver.Environment>
    {
        private static readonly Context.Key<AspectWeaver> CONTEXT_KEY = new Context.Key<AspectWeaver>();

        public static AspectWeaver instance(Context ctx) =>
            ctx.tryGet(CONTEXT_KEY, out var instance) ? instance : new AspectWeaver(ctx);

        private readonly Symtab symtab;

        private AspectWeaver(Context ctx)
        {
            ctx.put(CONTEXT_KEY, this);

            symtab = Symtab.instance(ctx);
        }

        public IList<CompilationUnit> main(IList<CompilationUnit> compilationUnits)
        {
            scan(compilationUnits, null);
            return compilationUnits;
        }

        public override object visitCompilationUnit(CompilationUnit compilationUnit, Environment env)
        {
            return scan(compilationUnit.declarations, env);
        }

        public override object visitMethodDef(MethodDef method, Environment env)
        {
            if (method.annotations.Count > 0) {
                Expression[] afterAspects = new Expression[method.annotations.Count];
                for (int i = 0; i < method.annotations.Count; i++) {
                    Annotation ann = method.annotations[i];

                    MethodInvocation afterInvocation = makeAfterInvokation(method, ann);

                    afterAspects[i] = afterInvocation;
                }
                weaveMethod(method, new Environment(afterAspects));

                if (method.exitsNormally) {
                    var ret = new ReturnStatement(0, 0, 0, 0, null);
                    ret.afterAspects = afterAspects;
                    method.body.statements.Add(ret);
                }
            }
            return null;
        }

        private MethodInvocation makeAfterInvokation(MethodDef method, Annotation ann)
        {
            LiteralExpression methodName = new LiteralExpression(0, 0, 0, 0, TypeTag.STRING, method.name);
            methodName.type = symtab.stringType.constType(method.name);

            IList<Expression> args = CollectionUtils
                .singletonList<Expression>(methodName);

            MethodInvocation afterInvocation = new MethodInvocation(0, 0, 0, 0, ann.symbol.afterMethod.name, args);
            afterInvocation.methodSym = ann.symbol.afterMethod;
            afterInvocation.type = afterInvocation.methodSym.type.ReturnType;
            
            return afterInvocation;
        }

        private void weaveMethod(MethodDef method, Environment env)
        {
            scan(method.body.statements, env);
        }

        public override object visitIf(If @if, Environment env)
        {
            scan(@if.thenPart, env);
            scan(@if.elsePart, env);
            return null;
        }

        public override object visitWhileLoop(WhileStatement whileStatement, Environment env) =>
            scan(whileStatement.body, env);

        public override object visitBlock(Block block, Environment env) => scan(block.statements, env);
        public override object visitDo(DoStatement doStatement, Environment env) => scan(doStatement.body, env);
        public override object visitForLoop(ForLoop forLoop, Environment env) => scan(forLoop.body, env);
        public override object visitSwitch(Switch @switch, Environment env) => scan(@switch.cases, env);
        public override object visitCase(Case @case, Environment env) => scan(@case.statements, env);

        public override object visitReturn(ReturnStatement returnStatement, Environment env)
        {
            returnStatement.afterAspects = env.afterAspects;
            return null;
        }

        public override object visit(Tree node, Environment arg) => null;

        public class Environment
        {
            public IList<Expression> afterAspects;

            public Environment(IList<Expression> afterAspects)
            {
                this.afterAspects = afterAspects;
            }
        }
    }
}
