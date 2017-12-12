using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

using mj.compiler.main;
using mj.compiler.resources;
using mj.compiler.tree;
using mj.compiler.utils;

namespace mj.compiler.symbol
{
    public class FlowAnalysis
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
                    new ReachabilityAnalyzer(log).scan(tree);
                } finally {
                    log.useSource(prevSource);
                }
            }
            return compilationUnits;
        }

        /// <summary>
        /// Each visitor method returns true if the statement can
        /// complete normally, which determines if the follwing
        /// statements are reachable.
        /// </summary>
        private class ReachabilityAnalyzer : AstVisitor<ReachabilityAnalyzer.Outcome>
        {
            private readonly Log log;

            public ReachabilityAnalyzer(Log log)
            {
                this.log = log;
            }

            public Outcome analyze(StatementNode tree) => tree.accept(this);

            public override Outcome visitCompilationUnit(CompilationUnit compilationUnit)
            {
                scan(compilationUnit.methods);
                // unused dummy return
                return Outcome.COMPLETES_NORMALLY;
            }

            public override Outcome visitMethodDef(MethodDef method)
            {
                Outcome outcome = this.analyze(method.body);
                if (outcome.HasFlag(Outcome.COMPLETES_NORMALLY) &&
                    !method.symbol.type.ReturnType.IsVoid) {
                    log.error(method.Pos, messages.missingReturnStatement);
                }
                // unused dummy return
                return outcome;
            }

            public override Outcome visitBlock(Block block) => analyze(block.statements);

            /* If a statement in a list of statements cannot complete
             * normally, than the list itself does not complete normally.
             * All other possible outcomes are preserved and returned.
             */
            private Outcome analyze(IList<StatementNode> stats)
            {
                if (stats.Count == 0) {
                    return Outcome.COMPLETES_NORMALLY;
                }

                Outcome total = 0;
                for (var i = 0; i < stats.Count; i++) {
                    Outcome res = analyze(stats[i]);
                    total |= res;
                    if (!res.HasFlag(Outcome.COMPLETES_NORMALLY)) {
                        int next = i + 1;
                        if (next < stats.Count) {
                            log.error(stats[next].Pos, messages.unreachableCodeDetected);
                        }
                        // remove completes normally from total
                        return total & ~Outcome.COMPLETES_NORMALLY;
                    }
                }
                return total;
            }

            /* "If" statement joins outcomes from the two branches.
             * If there is no "else" branch, than the "If" can 
             * complete normally.
             */
            public override Outcome visitIf(If ifStat)
            {
                Outcome outcome = analyze(ifStat.thenPart);
                if (ifStat.elsePart != null) {
                    outcome |= analyze(ifStat.elsePart);
                } else {
                    outcome |= Outcome.COMPLETES_NORMALLY;
                }
                return outcome;
            }

            /* If condition is not constant true the loop can complete
             * normally by not entering the loop body.
             */
            public override Outcome visitWhile(WhileStatement whileStat)
            {
                Outcome bodyOutcome = analyzeLoopBody(whileStat.body);
                if (!whileStat.condition.type.IsTrue) {
                    return bodyOutcome | Outcome.COMPLETES_NORMALLY;
                }
                return bodyOutcome;
            }

            /* If condition exists and is not constant true the loop 
             * can complete normally by not entering the loop body.
             */
            public override Outcome visitFor(ForLoop forLoop)
            {
                Outcome bodyOutcome = analyzeLoopBody(forLoop.body);
                if (forLoop.condition != null && !forLoop.condition.type.IsTrue) {
                    return bodyOutcome | Outcome.COMPLETES_NORMALLY;
                }
                return bodyOutcome;
            }

            /* "Do-While" loop always enters the loop body.
             */
            public override Outcome visitDo(DoStatement doStat)
            {
                return analyzeLoopBody(doStat.body);
            }

            /* Loop body statements "swollow" any "break" and "continue" and
             * replace them with "completes normally". Exit by return
             * is not examined.
             */
            private Outcome analyzeLoopBody(StatementNode body)
            {
                const Outcome breakCont = Outcome.EXITS_BY_CONTINUE | Outcome.EXITS_BY_BREAK;

                Outcome res = analyze(body);
                if ((res & breakCont) != 0) {
                    return (res & ~breakCont) | Outcome.COMPLETES_NORMALLY;
                }
                return res;
            }

            /* Empty switch completes notmally.
             * If any case can break then switch can complete normally.
             * If there is not default, the switch can complete normally
             * by not entering any case.
             */
            public override Outcome visitSwitch(Switch @switch)
            {
                if (@switch.cases.Count == 0) {
                    return Outcome.COMPLETES_NORMALLY;
                }

                bool hasDefault = false;
                Outcome total = 0;
                for (var i = 0; i < @switch.cases.Count; i++) {
                    Case @case = @switch.cases[i];
                    Outcome caseRes = analyze(@case.Statements);
                    total |= caseRes;
                    if (@case.expression == null) {
                        hasDefault = true;
                    }
                }

                if (total.HasFlag(Outcome.EXITS_BY_BREAK)) {
                    total &= ~Outcome.EXITS_BY_BREAK;
                    total |= Outcome.COMPLETES_NORMALLY;
                }
                
                if (!hasDefault) {
                    total |= Outcome.COMPLETES_NORMALLY;
                }
                
                return total;
            }

            public override Outcome visitBreak(Break @break) => Outcome.EXITS_BY_BREAK;
            public override Outcome visitContinue(Continue @continue) => Outcome.EXITS_BY_CONTINUE;
            public override Outcome visitReturn(ReturnStatement returnStatement) => Outcome.EXITS_BY_RETURN;
            public override Outcome visitExpresionStmt(ExpressionStatement expr) => Outcome.COMPLETES_NORMALLY;
            public override Outcome visitVarDef(VariableDeclaration varDef) => Outcome.COMPLETES_NORMALLY;

            [Flags]
            public enum Outcome
            {
                COMPLETES_NORMALLY = 0x1,
                EXITS_BY_BREAK = 0x2,
                EXITS_BY_CONTINUE = 0x4,
                EXITS_BY_RETURN = 0x8,
            }
        }

        private class VariableAssignmentAnalyzer : AstVisitor
        {
            private readonly Log log;

            public VariableAssignmentAnalyzer(Log log)
            {
                this.log = log;
            }
        }
    }
}
