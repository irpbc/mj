using mj.compiler.tree;

namespace mj.compiler.codegen
{
    public class JumpPatcher : AstVisitor
    {
        public override void visitBreak(Break @break)
        {
            @break.instruction.SetOperand(0, @break.target.BreakBlock);
        }

        public override void visitContinue(Continue @continue)
        {
            @continue.instruction.SetOperand(0, @continue.target.ContinueBlock);
        }

        public override void visitIf(If @if)
        {
            scan(@if.thenPart);
            scan(@if.elsePart);
        }

        public override void visitWhile(WhileStatement whileStatement) => scan(whileStatement.body);
        public override void visitFor(ForLoop forLoop) => scan(forLoop.body);
        public override void visitDo(DoStatement doStatement) => scan(doStatement.body);
        public override void visitSwitch(Switch @switch) => scan(@switch.cases);
        public override void visitCase(Case @case) => scan(@case.statements);
        public override void visitBlock(Block block) => scan(block.statements);

        public override void visit(Tree node) { }
    }
}
