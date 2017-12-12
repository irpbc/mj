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
        private class ReachabilityAnalyzer : AstVisitor<ReachabilityAnalyzer.Result>
        {
            private readonly Log log;

            public ReachabilityAnalyzer(Log log)
            {
                this.log = log;
            }

            public Result analyze(StatementNode tree) => tree.accept(this);

            public override Result visitCompilationUnit(CompilationUnit compilationUnit)
            {
                scan(compilationUnit.methods);
                return Result.COMPLETES_NORMALLY;
            }

            public override Result visitMethodDef(MethodDef method)
            {
                Result result = this.analyze(method.body);
                if (result.HasFlag(Result.COMPLETES_NORMALLY) &&
                    !method.symbol.type.ReturnType.IsVoid) {
                    log.error(method.Pos, messages.missingReturnStatement);
                }
                return result;
            }

            public override Result visitBlock(Block block)
            {
                return analyze(block.statements);
            }

            private Result analyze(IList<StatementNode> stats)
            {
                for (var i = 0; i < stats.Count; i++) {
                    Result res = analyze(stats[i]);
                    if (!res.HasFlag(Result.COMPLETES_NORMALLY)) {
                        int next = i + 1;
                        if (next < stats.Count) {
                            log.error(stats[next].Pos, messages.unreachableCodeDetected);
                        }
                        return res;
                    }
                }
                return Result.COMPLETES_NORMALLY;
            }

            public override Result visitIf(If ifStat)
            {
                Result result = analyze(ifStat.thenPart);
                if (ifStat.elsePart != null) {
                    result |= analyze(ifStat.elsePart);
                } else {
                    result |= Result.COMPLETES_NORMALLY;
                }
                return result;
            }

            public override Result visitWhile(WhileStatement whileStat) => analyzeLoopBody(whileStat.body);
            public override Result visitDo(DoStatement doStat) => analyzeLoopBody(doStat.body);
            public override Result visitFor(ForLoop forLoop) => analyzeLoopBody(forLoop.body);

            private Result analyzeLoopBody(StatementNode body)
            {
                Result res = analyze(body);
                if (res == Result.EXITS_BY_RETURN) {
                    return Result.EXITS_BY_RETURN;
                }
                return Result.COMPLETES_NORMALLY;
            }

            public override Result visitSwitch(Switch @switch)
            {
                if (@switch.cases.Count == 0) {
                    return Result.COMPLETES_NORMALLY;
                }

                Result total = 0;
                for (var i = 0; i < @switch.cases.Count; i++) {
                    Case @case = @switch.cases[i];
                    Result caseRes = analyze(@case.Statements);
                    total |= caseRes;
                }

                if (total.HasFlag(Result.EXITS_BY_BREAK)) {
                    total &= ~Result.EXITS_BY_BREAK;
                    total |= Result.COMPLETES_NORMALLY;
                }
                return total;
            }

            public override Result visitBreak(Break @break) => Result.EXITS_BY_BREAK;
            public override Result visitContinue(Continue @continue) => Result.EXITS_BY_CONTINUE;

            public override Result visitReturn(ReturnStatement returnStatement) =>
                Result.EXITS_BY_RETURN;

            public override Result visitExpresionStmt(ExpressionStatement expr) =>
                Result.COMPLETES_NORMALLY;

            public override Result visitVarDef(VariableDeclaration varDef) => Result.COMPLETES_NORMALLY;

            [Flags]
            public enum Result
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
