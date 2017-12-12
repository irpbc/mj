using System;
using System.Collections.Generic;
using System.Linq;

using mj.compiler.main;
using mj.compiler.resources;
using mj.compiler.tree;
using mj.compiler.utils;

using static mj.compiler.symbol.Scope;
using static mj.compiler.symbol.Symbol;

namespace mj.compiler.symbol
{
    public class TypeAnalysis : AstVisitor<Type, TypeAnalysis.Environment>
    {
        private static readonly Context.Key<TypeAnalysis> CONTEXT_KEY = new Context.Key<TypeAnalysis>();

        public static TypeAnalysis instance(Context ctx) =>
            ctx.tryGet(CONTEXT_KEY, out var instance) ? instance : new TypeAnalysis(ctx);

        private readonly Symtab symtab;
        private readonly Log log;
        private readonly Typings typings;
        private readonly Check check;
        private readonly Operators operators;

        public TypeAnalysis(Context ctx)
        {
            ctx.put(CONTEXT_KEY, this);

            symtab = Symtab.instance(ctx);
            log = Log.instance(ctx);
            typings = Typings.instance(ctx);
            check = Check.instance(ctx);
            operators = Operators.instance(ctx);
        }

        public IList<CompilationUnit> main(IList<CompilationUnit> compilationUnits)
        {
            Environment env = new Environment {scope = symtab.topLevelSymbol.topScope};
            foreach (CompilationUnit tree in compilationUnits) {
                SourceFile prevSource = log.useSource(tree.sourceFile);
                try {
                    analyze(tree, env);
                } finally {
                    log.useSource(prevSource);
                }
            }
            return compilationUnits;
        }

        private void analyze<T>(IList<T> trees, Environment env) where T : Tree
        {
            foreach (T tree in trees) {
                analyze(tree, env);
            }
        }

        private void analyze(Tree tree, Environment env) => tree.accept(this, env);

        private Type analyzeExpr(Expression expr, Environment env)
        {
            Type type = expr.accept(this, env);
            expr.type = type;
            return type;
        }

        public override Type visitCompilationUnit(CompilationUnit compilationUnit, Environment env)
        {
            analyze(compilationUnit.methods, env);

            return null;
        }

        public override Type visitMethodDef(MethodDef method, Environment env)
        {
            analyze(method.body.statements, new Environment {
                enclMethod = method.symbol,
                scope = method.symbol.scope,
                parent = env
            });
            // probaly unused
            return method.symbol.type;
        }

        public override Type visitBlock(Block block, Environment env)
        {
            analyze(block.statements, env.subScope(block));
            return null;
        }

        public override Type visitVarDef(VariableDeclaration varDef, Environment env)
        {
            string name = varDef.name;
            Type declaredType = varDef.type.accept(this, env);
            Expression init = varDef.init;

            VarSymbol varSymbol = new VarSymbol(Kind.LOCAL, name, declaredType, env.enclMethod);
            varDef.symbol = varSymbol;
            if (init != null) {
                Type initType = analyzeExpr(init, env);
                if (!typings.isAssignableFrom(declaredType, initType)) {
                    log.error(varDef.Pos, messages.varInitTypeMismatch, name, initType, declaredType);
                }
            }

            if (check.checkUniqueLocalVar(varDef.Pos, varSymbol, env.scope)) {
                env.scope.enter(varSymbol);
            }

            // probably unused
            return declaredType;
        }

        public override Type visitIf(If @if, Environment env)
        {
            Type conditionType = analyzeExpr(@if.condition, env);
            if (!isBoolean(conditionType)) {
                log.error(@if.condition.Pos, messages.ifConditonType);
            }

            analyze(@if.thenPart, env);
            if (@if.elsePart != null) {
                analyze(@if.elsePart, env);
            }

            return null;
        }

        public override Type visitWhileLoop(WhileStatement whileStatement, Environment outerEnv)
        {
            Type conditionType = analyzeExpr(whileStatement.condition, outerEnv);
            if (!isBoolean(conditionType)) {
                log.error(whileStatement.condition.Pos, messages.whileConditonType);
            }

            Environment whileEnv = outerEnv.subScope(whileStatement);
            analyze(whileStatement.body, whileEnv);

            return null;
        }

        public override Type visitDo(DoStatement doStatement, Environment outerEnv)
        {
            Environment doEnv = outerEnv.subScope(doStatement);
            analyze(doStatement.body, doEnv);

            Type conditionType = analyzeExpr(doStatement.condition, outerEnv);
            if (!isBoolean(conditionType)) {
                log.error(doStatement.condition.Pos, messages.whileConditonType);
            }

            return null;
        }

        private static bool isBoolean(Type type) => type.IsBoolean || type.IsError;

        public override Type visitForLoop(ForLoop forLoop, Environment env)
        {
            Environment forEnv = env;
            if (forLoop.init.Any(st => st is VariableDeclaration)) {
                forEnv = env.subScope(forLoop);
            }
            foreach (StatementNode st in forLoop.init) {
                analyze(st, forEnv);
            }
            if (forLoop.condition != null) {
                Type conditionType = analyzeExpr(forLoop.condition, forEnv);
                if (!isBoolean(conditionType)) {
                    log.error(forLoop.condition.Pos, messages.ifConditonType);
                }
            }
            foreach (Expression exp in forLoop.update) {
                analyze(exp, forEnv);
            }
            analyze(forLoop.body, forEnv);
            return null;
        }

        public override Type visitSwitch(Switch @switch, Environment env)
        {
            Type selectorType = analyzeExpr(@switch.selector, env);
            if (!selectorType.IsIntegral && !selectorType.IsError) {
                log.error(@switch.selector.Pos, messages.switchSelectorType);
            }

            Environment switchEnv = env.subScope(@switch);
            IList<Case> cases = @switch.cases;
            HashSet<object> caseValues = new HashSet<object>();
            bool hasDefault = false;
            for (var i = 0; i < cases.Count; i++) {
                Case @case = cases[i];
                if (@case.expression != null) {
                    Type caseExprType = analyzeExpr(@case.expression, env);
                    if (caseExprType.ConstValue == null || caseExprType.BaseType != selectorType) {
                        log.error(@case.expression.Pos, messages.caseExpressionType);
                    } else if (!caseValues.Add(caseExprType.ConstValue)) {
                        log.error(@case.expression.Pos, messages.duplicateCaseLabels);
                    }
                } else if (hasDefault) {
                    log.error(@case.Pos, messages.duplicateCaseLabels);
                } else {
                    hasDefault = true;
                }

                analyze(@case.Statements, switchEnv);
            }

            return null;
        }

        public override Type visitCase(Case @case, Environment env) => throw new InvalidOperationException();

        public override Type visitMethodInvoke(MethodInvocation invocation, Environment env)
        {
            // analyze argument expressions
            IList<Type> argTypes = invocation.args.convert(arg => analyzeExpr(arg, env));

            // resolve method
            string name = invocation.methodName;
            MethodSymbol msym = (MethodSymbol)env.scope.findFirst(name, s => s.kind == Kind.MTH);
            if (msym == null) {
                log.error(invocation.Pos, messages.methodNotDefined, name);
                // we dont need any validation
                invocation.type = symtab.errorType;
                return symtab.errorType;
            }

            // check argument count
            IList<VarSymbol> paramSyms = msym.parameters;
            if (argTypes.Count != paramSyms.Count) {
                log.error(invocation.Pos, messages.wrongNumberOfArgs, name, paramSyms.Count, argTypes.Count);
            }

            // check argument types
            int count = Math.Min(argTypes.Count, paramSyms.Count);
            for (int i = 0; i < count; i++) {
                VarSymbol paramSym = paramSyms[i];
                Type argType = argTypes[i];

                // we don't consider implicit numeric conversions for now
                if (!typings.isAssignableFrom(paramSym.type, argType)) {
                    log.error(invocation.Pos, messages.paramTypeMismatch, msym.name);
                }
            }

            invocation.type = msym.type.ReturnType;
            return msym.type.ReturnType;
        }

        public override Type visitExpresionStmt(ExpressionStatement expr, Environment env)
        {
            return analyzeExpr(expr.expression, env);
        }

        public override Type visitPrimitiveType(PrimitiveTypeNode prim, Environment env)
        {
            return symtab.typeForTag(prim.type);
        }

        public override Type visitLiteral(LiteralExpression literal, Environment env)
        {
            return symtab.typeForTag(literal.type).constType(literal.value);
        }

        public override Type visitIdent(Identifier ident, Environment env)
        {
            Symbol varSym = env.scope.findFirst(ident.name, s => s.kind.hasAny(Kind.VAR));
            if (varSym == null) {
                log.error(ident.Pos, messages.undefinedVariable, ident.name);
                varSym = symtab.errorSymbol;
            }
            ident.symbol = varSym;
            return varSym.type;
        }

        public override Type visitReturn(ReturnStatement returnStatement, Environment env)
        {
            Type returnType = env.enclMethod.type.ReturnType;
            Expression expr = returnStatement.value;
            if (expr != null) {
                Type exprType = analyzeExpr(expr, env);
                if (returnType == symtab.voidType) {
                    log.error(returnStatement.Pos, messages.returnVoidMethod);
                } else if (!typings.isAssignableFrom(returnType, exprType)) {
                    log.error(expr.Pos, messages.returnTypeMismatch);
                }
            } else if (returnType != symtab.voidType) {
                log.error(returnStatement.Pos, messages.returnNonVoidMethod);
            }

            return env.enclMethod.type.ReturnType;
        }

        public override Type visitConditional(ConditionalExpression conditional, Environment env)
        {
            Type condType = analyzeExpr(conditional.condition, env);
            Type trueType = analyzeExpr(conditional.ifTrue, env);
            Type falseType = analyzeExpr(conditional.ifFalse, env);

            if (condType != symtab.booleanType) {
                log.error(conditional.condition.Pos, messages.conditionalBoolean);
            }

            if (!unifyConditionalTypes(trueType, falseType, out Type result)) {
                log.error(conditional.ifFalse.Pos, messages.conditionalMismatch);
            }
            conditional.type = result;
            return result;
        }

        private bool unifyConditionalTypes(Type left, Type right, out Type result)
        {
            if (left.IsError || right.IsError) {
                result = symtab.errorType;
                return true;
            }
            if (typings.isAssignableFrom(left, right)) {
                result = left;
                return true;
            }
            if (typings.isAssignableFrom(right, left)) {
                result = right;
                return false;
            }
            result = symtab.errorType;
            return false;
        }

        public override Type visitContinue(Continue cont, Environment env)
        {
            // Search for an enclosing loop
            for (; env.enclStatement != null; env = env.parent) {
                if (env.enclStatement.Tag.isLoop()) {
                    return null;
                }
            }
            log.error(cont.Pos, messages.continueNotInLoop);
            return null;
        }

        public override Type visitBreak(Break @break, Environment env)
        {
            // Search for an enclosing loop
            for (; env.enclStatement != null; env = env.parent) {
                if (env.enclStatement.Tag.isLoop() || env.enclStatement.Tag == Tag.SWITCH) {
                    return null;
                }
            }
            log.error(@break.Pos, messages.breakNotInLoopSwitch);
            return null;
        }

        public override Type visitUnary(UnaryExpressionNode unary, Environment env)
        {
            Type operandType = analyzeExpr(unary.operand, env);
            if (unary.opcode.isIncDec() && !unary.operand.IsLValue) {
                log.error(unary.Pos, messages.incDecArgument);
            }

            OperatorSymbol op = operators.resolveUnary(unary.Pos, unary.opcode, operandType);
            unary.operatorSym = op;
            return op.type.ReturnType;
        }

        public override Type visitBinary(BinaryExpressionNode binary, Environment env)
        {
            Type leftType = analyzeExpr(binary.left, env);
            Type rightType = analyzeExpr(binary.right, env);

            OperatorSymbol op = operators.resolveBinary(binary.Pos, binary.opcode, leftType, rightType);
            binary.operatorSym = op;
            return op.type.ReturnType;
        }

        public override Type visitAssign(AssignNode assign, Environment env)
        {
            bool lValueError = !assign.left.IsLValue;
            if (lValueError) {
                log.error(assign.Pos, messages.assignmentLHS);
            }

            Type lType = analyzeExpr(assign.left, env);
            Type rType = analyzeExpr(assign.right, env);

            if (!lValueError && !typings.isAssignableFrom(lType, rType)) {
                log.error(assign.Pos, messages.assignmentUncompatible);
            }

            return lType;
        }

        public override Type visitCompoundAssign(CompoundAssignNode compAssign, Environment env)
        {
            bool lValueError = !compAssign.left.IsLValue;
            if (lValueError) {
                log.error(compAssign.Pos, messages.assignmentLHS);
            }

            Type lType = analyzeExpr(compAssign.left, env);
            Type rType = analyzeExpr(compAssign.right, env);

            OperatorSymbol op = operators.resolveBinary(compAssign.Pos, compAssign.opcode.baseOperator(), lType, rType);
            return op.type.ReturnType;
        }

        /// do nothing for unhandled nodes
        public override Type visit(Tree node, Environment arg) => null;

        /// <summary>
        /// A holder of contextual information needed by the visitor methods
        /// of this compiler stage. Environments are chained like scopes, but
        /// contain some more information, like the current enclosing method 
        /// and the current enclosing control flow statement.
        /// </summary>
        public class Environment
        {
            public Environment parent;
            public WriteableScope scope;

            /// Needed for "continue" and "break" target resolution
            public StatementNode enclStatement;

            /// Needed for return statement checking
            public MethodSymbol enclMethod;

            /// <summary>
            /// Create a new env with this env as parent, with a subscope
            /// and a new enclosing statement.
            /// </summary>
            public Environment subScope(StatementNode enclStmt)
            {
                return new Environment {
                    parent = this,
                    scope = scope.subScope(),
                    enclStatement = enclStmt,
                    enclMethod = enclMethod
                };
            }
        }
    }
}
