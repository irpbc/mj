using System;
using System.Collections.Generic;

using mj.compiler.main;
using mj.compiler.resources;
using mj.compiler.tree;
using mj.compiler.utils;

using static mj.compiler.symbol.Symbol;

namespace mj.compiler.symbol
{
    /// <summary>
    /// Control flow analysis. Checks if all statements are reachable,
    /// and if all the variables are assigned before use.
    /// </summary>
    /// 
    /// Each visitor method returns possible ways a statement can exit,
    /// Which determines weather the following statement in a list is
    /// reachable. Each kind of statement has its own behaviour.
    /// 
    /// An Environment instance holds the assigned variables durring the
    /// pass. The Environment can be split into branches and merged when
    /// control flow diverges (if, switch, loops).
    /// 
    public class FlowAnalysis : AstVisitor<FlowAnalysis.Exit, FlowAnalysis.Environment>
    {
        private static readonly Context.Key<FlowAnalysis> CONTEXT_KEY = new Context.Key<FlowAnalysis>();

        public static FlowAnalysis instance(Context ctx) =>
            ctx.tryGet(CONTEXT_KEY, out var instance) ? instance : new FlowAnalysis(ctx);

        private readonly Log log;

        private FlowAnalysis(Context ctx)
        {
            ctx.put(CONTEXT_KEY, this);

            log = Log.instance(ctx);
        }

        public IList<CompilationUnit> main(IList<CompilationUnit> compilationUnits)
        {
            foreach (CompilationUnit tree in compilationUnits) {
                SourceFile prevSource = log.useSource(tree.sourceFile);
                try {
                    scan(tree, null);
                } finally {
                    log.useSource(prevSource);
                }
            }
            return compilationUnits;
        }

        public Exit analyze(StatementNode tree, Environment env) => tree.accept(this, env);

        private void analyzeExpr(Expression expr, Environment env) => expr.accept(this, env);

        public override Exit visitCompilationUnit(CompilationUnit compilationUnit, Environment env)
        {
            scan(compilationUnit.declarations, env);
            return 0;
        }

        public override Exit visitClassDef(ClassDef classDef, Environment arg) => 0;

        public override Exit visitMethodDef(MethodDef method, Environment env)
        {
            Environment methodEnv = new Environment();
            Exit exit = this.analyze(method.body, methodEnv);
            if (exit.HasFlag(Exit.NORMALLY)) {
                method.exitsNormally = true;
                if (!method.symbol.type.ReturnType.IsVoid) {
                    log.error(method.Pos, messages.missingReturnStatement);
                }
            }

            return 0;
        }

        /*public override Exit visitAspectDef(AspectDef aspect, Environment env)
        {
            scan(aspect.after, env);
            return 0;
        }*/

        public override Exit visitBlock(Block block, Environment env) => analyze(block.statements, env);

        /* If a statement in a list of statements cannot complete
         * normally, than the list itself does not complete normally.
         * All other possible outcomes are preserved and returned.
         */
        private Exit analyze(IList<StatementNode> stats, Environment env)
        {
            if (stats.Count == 0) {
                return Exit.NORMALLY;
            }

            Exit total = 0;
            for (var i = 0; i < stats.Count; i++) {
                Exit res = analyze(stats[i], env);
                total |= res;
                if (!res.HasFlag(Exit.NORMALLY)) {
                    int next = i + 1;
                    if (next < stats.Count) {
                        log.error(stats[next].Pos, messages.unreachableCodeDetected);
                    }
                    // remove completes normally from total
                    return total & ~Exit.NORMALLY;
                }
            }
            return total;
        }

        /* "If" statement joins outcomes from the two branches.
         * If there is no "else" branch, than the "If" can 
         * complete normally.
         *
         * The Environment is split into two branches and merged afterward.
         */
        public override Exit visitIf(If ifStat, Environment env)
        {
            analyzeExpr(ifStat.condition, env);

            if (ifStat.condition.type.IsTrue) {
                return analyze(ifStat.thenPart, env);
            }

            if (ifStat.elsePart == null) {
                Environment ifEnv = env.subEnvironment();
                return analyze(ifStat.thenPart, ifEnv) | Exit.NORMALLY;
            }

            if (ifStat.condition.type.IsFalse) {
                return analyze(ifStat.elsePart, env);
            }

            var (whenTrue, whenFalse) = env.split();
            Exit exit = analyze(ifStat.thenPart, whenTrue) |
                        analyze(ifStat.elsePart, whenFalse);
            whenTrue.merge(whenFalse);
            return exit;
        }

        /* If condition is not constant true the loop can complete
         * normally by not entering the loop body.
         */
        public override Exit visitWhileLoop(WhileStatement whileStat, Environment env)
        {
            analyzeExpr(whileStat.condition, env);

            // If condition is true than we always enter the body,
            // so no need to split the environment.
            if (whileStat.condition.type.IsTrue) {
                return analyzeLoopBody(whileStat.body, env);
            }

            Environment bodyEnv = env.subEnvironment();
            return analyzeLoopBody(whileStat.body, bodyEnv) | Exit.NORMALLY;
        }

        /* If condition exists and is not constant true the loop 
         * can complete normally by not entering the loop body.
         */
        public override Exit visitForLoop(ForLoop forLoop, Environment env)
        {
            analyze(forLoop.init, env);
            analyzeExpr(forLoop.condition, env);

            if (forLoop.condition == null || forLoop.condition.type.IsTrue) {
                return analyzeLoopBody(forLoop.body, env);
            }

            Environment loopEnv = env.subEnvironment();
            Exit bodyExit = analyzeLoopBody(forLoop.body, loopEnv);

            return bodyExit | Exit.NORMALLY;
        }

        /* "Do-While" loop always enters the loop body.
         */
        public override Exit visitDo(DoStatement doStat, Environment env)
        {
            Exit bodyExit = analyzeLoopBody(doStat.body, env);
            analyzeExpr(doStat.condition, env);
            return bodyExit;
        }

        /* Loop body statements "swollow" any "break" and "continue" and
         * replace them with "completes normally". Exit by return
         * is not examined.
         */
        private Exit analyzeLoopBody(StatementNode body, Environment env)
        {
            const Exit breakCont = Exit.CONTINUE | Exit.BREAK;

            Exit res = analyze(body, env);
            if ((res & breakCont) != 0) {
                return (res & ~breakCont) | Exit.NORMALLY;
            }
            return res;
        }

        /* Empty switch completes notmally.
         * If any case can break then switch can complete normally.
         * If there is not default, the switch can complete normally
         * by not entering any case.
         */
        public override Exit visitSwitch(Switch @switch, Environment env)
        {
            if (@switch.cases.Count == 0) {
                return Exit.NORMALLY;
            }

            analyzeExpr(@switch.selector, env);

            bool hasDefault = false;
            Exit total = 0;
            IList<Environment> caseEnvs = env.split(@switch.cases.Count);
            for (int i = 0; i < @switch.cases.Count; i++) {
                Case @case = @switch.cases[i];
                Exit caseRes = analyze(@case.statements, env);
                total |= caseRes;
                if (@case.expression == null) {
                    hasDefault = true;
                }
            }

            if (total.HasFlag(Exit.BREAK)) {
                total &= ~Exit.BREAK;
                total |= Exit.NORMALLY;
            }

            if (!hasDefault) {
                total |= Exit.NORMALLY;
            } else {
                Environment.merge(caseEnvs);
            }

            return total;
        }

        public override Exit visitBreak(Break @break, Environment env) => Exit.BREAK;
        public override Exit visitContinue(Continue @continue, Environment env) => Exit.CONTINUE;

        public override Exit visitExpresionStmt(ExpressionStatement expr, Environment env)
        {
            analyzeExpr(expr.expression, env);
            return Exit.NORMALLY;
        }

        public override Exit visitAssign(AssignNode expr, Environment env)
        {
            analyzeExpr(expr.right, env);
            if (expr.left is Identifier id) {
                VarSymbol varSym = id.symbol;
                if (varSym.kind == Kind.LOCAL) {
                    env.assigned(varSym);
                }
            }
            return 0;
        }

        public override Exit visitVarDef(VariableDeclaration varDef, Environment env)
        {
            if (varDef.init != null) {
                analyzeExpr(varDef.init, env);
                env.assigned(varDef.symbol);
            }
            return Exit.NORMALLY;
        }

        public override Exit visitBinary(BinaryExpressionNode expr, Environment env)
        {
            analyzeExpr(expr.left, env);
            analyzeExpr(expr.right, env);

            return 0;
        }

        public override Exit visitUnary(UnaryExpressionNode expr, Environment env)
        {
            analyzeExpr(expr.operand, env);
            return 0;
        }

        public override Exit visitCompoundAssign(CompoundAssignNode expr, Environment env)
        {
            analyzeExpr(expr.left, env);
            analyzeExpr(expr.right, env);
            return 0;
        }

        public override Exit visitIdent(Identifier ident, Environment env)
        {
            VarSymbol varSym = ident.symbol;
            if (varSym.kind == Kind.LOCAL && !env.isAssigned(varSym)) {
                log.error(ident.Pos, messages.unassignedVariable, ident.name);
            }
            return 0;
        }

        public override Exit visitMethodInvoke(MethodInvocation methodInvocation, Environment env)
        {
            for (var i = 0; i < methodInvocation.args.Count; i++) {
                analyzeExpr(methodInvocation.args[i], env);
            }
            return 0;
        }

        public override Exit visitConditional(ConditionalExpression conditional, Environment env)
        {
            analyzeExpr(conditional.condition, env);

            var (whenTrue, whenFalse) = env.split();

            analyzeExpr(conditional.ifTrue, whenTrue);
            analyzeExpr(conditional.ifFalse, whenFalse);

            whenTrue.merge(whenFalse);

            return 0;
        }

        public override Exit visitLiteral(LiteralExpression literal, Environment arg) => 0;
        public override Exit visitSelect(Select select, Environment arg) => 0;
        public override Exit visitIndex(ArrayIndex index, Environment arg) => 0;
        public override Exit visitNewClass(NewClass newClass, Environment arg) => 0;
        public override Exit visitNewArray(NewArray newArray, Environment env) => 0;

        public override Exit visitReturn(ReturnStatement returnStatement, Environment env)
        {
            if (returnStatement.value != null) {
                analyzeExpr(returnStatement.value, env);
            }
            return Exit.RETURN;
        }

        [Flags]
        public enum Exit
        {
            NORMALLY = 0x1,
            BREAK = 0x2,
            CONTINUE = 0x4,
            RETURN = 0x8,
        }

        /// <summary>
        /// Within this analysis normal exit is esclusive with other kinds,
        /// because having a possibility of abnormal exit is sufficient to 
        /// disprove definite assignment of any variable within a list of 
        /// statements after the offending statement.
        /// </summary>
        public class Environment
        {
            private Environment parent;
            private ISet<VarSymbol> inits = new HashSet<VarSymbol>(5);

            public void assigned(VarSymbol varSym)
            {
                inits.Add(varSym);
            }

            public bool isAssigned(VarSymbol varSym)
            {
                bool contains = inits.Contains(varSym);
                if (contains) {
                    return true;
                }
                if (parent != null) {
                    return parent.isAssigned(varSym);
                }
                return false;
            }

            // Intersect variables from this (true) and other (false) 
            // branch and  add the result to the set of definitelly 
            // initialized variables
            public void merge(Environment whenFalse)
            {
                inits.IntersectWith(whenFalse.inits);
                parent.inits.UnionWith(inits);
            }

            public void merge()
            {
                parent.inits.UnionWith(inits);
            }

            public static void merge(IList<Environment> caseEnvs)
            {
                Environment first = caseEnvs[0];
                for (var i = 1; i < caseEnvs.Count; i++) {
                    first.inits.IntersectWith(caseEnvs[i].inits);
                }
                first.merge();
            }

            // Split the environment into true and false branches.
            // 
            public (Environment whenTrue, Environment whenFalse) split()
            {
                Environment whenTrue = new Environment {parent = this};
                Environment whenFalse = new Environment {parent = this};
                return (whenTrue, whenFalse);
            }

            // Split into n branches
            public IList<Environment> split(int numEnvs)
            {
                Environment[] envs = new Environment[numEnvs];
                for (var i = 0; i < numEnvs; i++) {
                    envs[i] = new Environment() {parent = this};
                }
                return envs;
            }

            public Environment subEnvironment()
            {
                return new Environment {parent = this};
            }
        }
    }
}
