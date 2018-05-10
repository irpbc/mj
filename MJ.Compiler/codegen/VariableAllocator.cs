using LLVMSharp;

using mj.compiler.tree;

namespace mj.compiler.codegen
{
    public class VariableAllocator : AstVisitor
    {
        private readonly LLVMBuilderRef builder;
        private readonly LLVMTypeResolver typeResolver;

        public VariableAllocator(LLVMBuilderRef builder, LLVMTypeResolver typeResolver)
        {
            this.builder = builder;
            this.typeResolver = typeResolver;
        }

        public override void visitVarDef(VariableDeclaration varDef)
        {
            varDef.symbol.llvmRef =
                LLVM.BuildAlloca(builder, varDef.symbol.type.accept(typeResolver), varDef.symbol.name);
        }

        public override void visitFor(ForLoop forLoop)
        {
            scan(forLoop.init);
            scan(forLoop.body);
        }

        public override void visitIf(If @if)
        {
            scan(@if.thenPart);
            scan(@if.elsePart);
        }

        public override void visitBlock(Block block) => scan(block.statements);
        public override void visitWhile(WhileStatement whileStatement) => scan(whileStatement.body);
        public override void visitDo(DoStatement doStatement) => scan(doStatement.body);
        public override void visitSwitch(Switch @switch) => scan(@switch.cases);
        public override void visitCase(Case @case) => scan(@case.statements);

        public override void visit(Tree node) { }
    }
}
