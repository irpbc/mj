using mj.compiler.tree;

namespace mj.compiler.codegen
{
    public class CallPatcher : AstVisitor
    {
        public override void visitMethodInvoke(MethodInvocation methodInvocation)
        {
            methodInvocation.instruction.SetOperand(0, methodInvocation.methodSym.llvmPointer);
        }

        public override void visitCompilationUnit(CompilationUnit compilationUnit) => scan(compilationUnit.methods);
        public override void visitMethodDef(MethodDef method) => scan(method.body.statements);

        public override void visitIf(If @if)
        {
            scan(@if.thenPart);
            scan(@if.elsePart);
        }

        public override void visitBinary(BinaryExpressionNode expr)
        {
            scan(expr.left);
            scan(expr.right);
        }

        public override void visitConditional(ConditionalExpression conditional)
        {
            scan(conditional.condition);
            scan(conditional.ifTrue);
            scan(conditional.ifFalse);
        }

        public override void visitWhile(WhileStatement whileStatement)
        {
            scan(whileStatement.condition);
            scan(whileStatement.body);
        }

        public override void visitFor(ForLoop forLoop)
        {
            scan(forLoop.init);
            scan(forLoop.condition);
            scan(forLoop.body);
            scan(forLoop.update);
        }

        public override void visitDo(DoStatement doStatement)
        {
            scan(doStatement.body);
            scan(doStatement.condition);
        }

        public override void visitSwitch(Switch @switch)
        {
            scan(@switch.selector);
            scan(@switch.cases);
        }

        public override void visitCase(Case @case) => scan(@case.statements);
        public override void visitBlock(Block block) => scan(block.statements);
        public override void visitUnary(UnaryExpressionNode expr) => scan(expr.operand);
        public override void visitAssign(AssignNode expr) => scan(expr.right);
        public override void visitCompoundAssign(CompoundAssignNode expr) => scan(expr.right);
        public override void visitReturn(ReturnStatement returnStatement) => scan(returnStatement.value);
        public override void visitExpresionStmt(ExpressionStatement expr) => scan(expr.expression);
        public override void visitVarDef(VariableDeclaration varDef) => scan(varDef.init);

        public override void visit(Tree node) { }
    }
}
