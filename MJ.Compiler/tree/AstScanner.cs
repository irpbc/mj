using System.Collections;
using System.Collections.Generic;

namespace mj.compiler.tree
{
    /*public class AstScanner : AstVisitor
    {
        public override void visitCompilationUnit(CompilationUnit compilationUnit)
        {
            scan(compilationUnit.methods);
        }

        public override void visitMethodDef(MethodDef method)
        {
            scan(method.returnType);
            scan(method.parameters);
            scan(method.body);
        }

        public override void visitVarDef(VariableDeclaration varDef)
        {
            scan(varDef.type);
            scan(varDef.init);
        }

        public override void visitBinary(BinaryExpressionNode expr)
        {
            scan(expr.left);
            scan(expr.right);
        }

        public override void visitUnary(UnaryExpressionNode expr)
        {
            scan(expr.operand);
        }

        public override void visitAssign(AssignNode expr)
        {
            scan(expr.left);
            scan(expr.right);
        }

        public override void visitCompoundAssign(CompoundAssignNode expr)
        {
            scan(expr.left);
            scan(expr.right);
        }

        public override void visitMethodInvoke(MethodInvocation methodInvocation)
        {
            scan(methodInvocation.args);
        }

        public override void visitReturn(ReturnStatement returnStatement)
        {
            scan(returnStatement.value);
        }

        public override void visitBlock(Block block)
        {
            scan(block.statements);
        }

        public override void visitIf(If @if)
        {
            scan(@if.condition);
            scan(@if.thenPart);
            scan(@if.elsePart);
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
            scan(forLoop.update);
            scan(forLoop.body);
        }

        public override void visitExpresionStmt(ExpressionStatement expr)
        {
            scan(expr.expression);
        }

        public override void visitDo(DoStatement doStatement)
        {
            scan(doStatement.body);
            scan(doStatement.condition);
        }

        public override void visitConditional(ConditionalExpression conditional)
        {
            scan(conditional.condition);
            scan(conditional.ifTrue);
            scan(conditional.ifFalse);
        }

        public override void visitSwitch(Switch @switch)
        {
            scan(@switch.selector);
            scan(@switch.cases);
        }

        public override void visitCase(Case @case)
        {
            scan(@case.expression);
            scan(@case.Statements);
        }
    }*/
}
