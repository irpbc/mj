namespace mj.compiler.parsing.ast
{
    public abstract class AstVisitor<T>
    {
        public T visit(AstNode node) => node.accept(this);
        
        public abstract T visitBinary(BinaryExpressionNode expr);
        public abstract T visitUnary(UnaryExpressionNode expr);
        public abstract T visitLiteral(LiteralExpressionNode literal);
        public abstract T visitMethodDef(MethodNode method);
        public abstract T visitParam(MethodParameter parameter);
        public abstract T visitLocalVar(LocalVariableDeclaration localVariableDeclaration);
        public abstract T visitCompilationUnit(CompilatioUnit compilatioUnit);
        public abstract T visitIdent(IdentifierNode identifierNode);
        public abstract T visitMethodInvoke(MethodInvocation methodInvocation);
        public abstract T visitReturn(ReturnStatement returnStatement);
        public abstract T visitBlock(Block block);
        public abstract T visitBreak(Break @break);
        public abstract T visitIf(If @if);
        public abstract T visitContinue(Continue @continue);
        public abstract T visitWhile(WhileStatement whileStatement);
        public abstract T visitFor(ForLoop forLoop);
        public abstract T visitExpresionStmt(ExpressionStatement expressionStatement);
        public abstract T visitDo(DoStatement doStatement);
        public abstract T visitConditional(ConditionalExpressionNode conditionalExpressionNode);
        public abstract T visitPrimitiveType(PrimitiveTypeNode primitiveTypeNode);
        public abstract T visitSwitch(Switch @switch);
        public abstract T visitCase(Case @case);
    }
}
