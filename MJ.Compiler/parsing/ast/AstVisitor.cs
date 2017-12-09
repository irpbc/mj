using System;

namespace mj.compiler.parsing.ast
{
    public abstract class AstVisitor<T>
    {
        public virtual T visitCompilationUnit(CompilationUnit compilationUnit) => visit(compilationUnit);
        public virtual T visitMethodDef(MethodDef method) => visit(method);
        public virtual T visitVarDef(VariableDeclaration varDef) => visit(varDef);
        public virtual T visitBinary(BinaryExpressionNode expr) => visit(expr);
        public virtual T visitUnary(UnaryExpressionNode expr) => visit(expr);
        public virtual T visitLiteral(LiteralExpression literal) => visit(literal);
        public virtual T visitIdent(Identifier ident) => visit(ident);
        public virtual T visitMethodInvoke(MethodInvocation methodInvocation) => visit(methodInvocation);
        public virtual T visitReturn(ReturnStatement returnStatement) => visit(returnStatement);
        public virtual T visitBlock(Block block) => visit(block);
        public virtual T visitBreak(Break @break) => visit(@break);
        public virtual T visitIf(If @if) => visit(@if);
        public virtual T visitContinue(Continue @continue) => visit(@continue);
        public virtual T visitWhile(WhileStatement whileStatement) => visit(whileStatement);
        public virtual T visitFor(ForLoop forLoop) => visit(forLoop);
        public virtual T visitExpresionStmt(ExpressionStatement expr) => visit(expr);
        public virtual T visitDo(DoStatement doStatement) => visit(doStatement);
        public virtual T visitConditional(ConditionalExpression conditional) => visit(conditional);
        public virtual T visitPrimitiveType(PrimitiveTypeNode prim) => visit(prim);
        public virtual T visitSwitch(Switch @switch) => visit(@switch);
        public virtual T visitCase(Case @case) => visit(@case);

        public virtual T visit(Tree node) => throw new InvalidOperationException();
    }

    public abstract class AstVisitor<T, A>
    {
        public virtual T visitCompilationUnit(CompilationUnit compilationUnit, A arg) => visit(compilationUnit, arg);
        public virtual T visitMethodDef(MethodDef method, A arg) => visit(method, arg);
        public virtual T visitVarDef(VariableDeclaration varDef, A arg) => visit(varDef, arg);
        public virtual T visitBlock(Block block, A arg) => visit(block, arg);
        public virtual T visitBinary(BinaryExpressionNode expr, A arg) => visit(expr, arg);
        public virtual T visitUnary(UnaryExpressionNode expr, A arg) => visit(expr, arg);
        public virtual T visitLiteral(LiteralExpression literal, A arg) => visit(literal, arg);
        public virtual T visitIdent(Identifier ident, A arg) => visit(ident, arg);
        public virtual T visitMethodInvoke(MethodInvocation methodInvocation, A arg) => visit(methodInvocation, arg);
        public virtual T visitConditional(ConditionalExpression conditional, A arg) => visit(conditional, arg);
        public virtual T visitReturn(ReturnStatement returnStatement, A arg) => visit(returnStatement, arg);
        public virtual T visitBreak(Break @break, A arg) => visit(@break, arg);
        public virtual T visitIf(If @if, A arg) => visit(@if, arg);
        public virtual T visitContinue(Continue @continue, A arg) => visit(@continue, arg);
        public virtual T visitWhileLoop(WhileStatement whileStatement, A arg) => visit(whileStatement, arg);
        public virtual T visitForLoop(ForLoop forLoop, A arg) => visit(forLoop, arg);
        public virtual T visitExpresionStmt(ExpressionStatement expr, A arg) => visit(expr, arg);
        public virtual T visitDo(DoStatement doStatement, A arg) => visit(doStatement, arg);
        public virtual T visitPrimitiveType(PrimitiveTypeNode prim, A arg) => visit(prim, arg);
        public virtual T visitSwitch(Switch @switch, A arg) => visit(@switch, arg);
        public virtual T visitCase(Case @case, A arg) => visit(@case, arg);

        public virtual T visit(Tree node, A arg) => throw new InvalidOperationException();
    }
}
