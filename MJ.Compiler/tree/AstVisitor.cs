using System;
using System.Collections.Generic;

namespace mj.compiler.tree
{
    public abstract class AstVisitor<T>
    {
        public virtual T visitCompilationUnit(CompilationUnit compilationUnit) => visit(compilationUnit);
        public virtual T visitStructDef(StructDef structDef) => visit(structDef);
        public virtual T visitFuncDef(FuncDef func) => visit(func);
        public virtual T visitVarDef(VariableDeclaration varDef) => visit(varDef);
        public virtual T visitBinary(BinaryExpressionNode expr) => visit(expr);
        public virtual T visitUnary(UnaryExpressionNode expr) => visit(expr);
        public virtual T visitAssign(AssignNode expr) => visit(expr);
        public virtual T visitCompoundAssign(CompoundAssignNode expr) => visit(expr);
        public virtual T visitLiteral(LiteralExpression literal) => visit(literal);
        public virtual T visitIdent(Identifier ident) => visit(ident);
        public virtual T visitSelect(Select select) => visit(select);
        public virtual T visitIndex(ArrayIndex index) => visit(index);
        public virtual T visitNewStruct(NewStruct newStruct) => visit(newStruct);
        public virtual T visitNewArray(NewArray newArray) => visit(newArray);
        public virtual T visitFuncInvoke(FuncInvocation funcInvocation) => visit(funcInvocation);
        public virtual T visitReturn(ReturnStatement returnStatement) => visit(returnStatement);
        public virtual T visitBlock(Block block) => visit(block);
        public virtual T visitBreak(Break @break) => visit(@break);
        public virtual T visitIf(If ifStat) => visit(ifStat);
        public virtual T visitContinue(Continue @continue) => visit(@continue);
        public virtual T visitWhile(WhileStatement whileStat) => visit(whileStat);
        public virtual T visitFor(ForLoop forLoop) => visit(forLoop);
        public virtual T visitExpresionStmt(ExpressionStatement expr) => visit(expr);
        public virtual T visitDo(DoStatement doStat) => visit(doStat);
        public virtual T visitConditional(ConditionalExpression conditional) => visit(conditional);
        public virtual T visitPrimitiveType(PrimitiveTypeNode prim) => visit(prim);
        public virtual T visitDeclaredType(DeclaredType declaredType) => visit(declaredType);
        public virtual T visitArrayType(ArrayTypeTree arrayType) => visit(arrayType);
        public virtual T visitSwitch(Switch @switch) => visit(@switch);
        public virtual T visitCase(Case @case) => visit(@case);

        public virtual T visit(Tree node) => throw new InvalidOperationException();

        public T scan<TT>(IList<TT> trees) where TT : Tree
        {
            for (var i = 0; i < trees.Count; i++) {
                scan(trees[i]);
            }
            return default;
        }

        public T scan(Tree tree)
        {
            if (tree != null) {
                return tree.accept(this);
            }
            return default;
        }
    }

    public abstract class AstVisitor<T, A>
    {
        public virtual T visitCompilationUnit(CompilationUnit compilationUnit, A arg) => visit(compilationUnit, arg);
        public virtual T visitStructDef(StructDef structDef, A arg) => visit(structDef, arg);
        public virtual T visitFuncDef(FuncDef func, A arg) => visit(func, arg);
        public virtual T visitVarDef(VariableDeclaration varDef, A arg) => visit(varDef, arg);
        public virtual T visitBlock(Block block, A arg) => visit(block, arg);
        public virtual T visitBinary(BinaryExpressionNode expr, A arg) => visit(expr, arg);
        public virtual T visitUnary(UnaryExpressionNode expr, A arg) => visit(expr, arg);
        public virtual T visitAssign(AssignNode expr, A arg) => visit(expr, arg);
        public virtual T visitCompoundAssign(CompoundAssignNode expr, A arg) => visit(expr, arg);
        public virtual T visitLiteral(LiteralExpression literal, A arg) => visit(literal, arg);
        public virtual T visitIdent(Identifier ident, A arg) => visit(ident, arg);
        public virtual T visitSelect(Select select, A arg) => visit(select, arg);
        public virtual T visitIndex(ArrayIndex index, A arg) => visit(index, arg);
        public virtual T visitNewStruct(NewStruct newStruct, A arg) => visit(newStruct, arg);
        public virtual T visitNewArray(NewArray newArray, A env) => visit(newArray, env);
        public virtual T visitFuncInvoke(FuncInvocation funcInvocation, A arg) => visit(funcInvocation, arg);
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
        public virtual T visitDeclaredType(DeclaredType declaredType, A arg) => visit(declaredType, arg);
        public virtual T visitArrayType(ArrayTypeTree arrayType, A arg) => visit(arrayType, arg);
        public virtual T visitSwitch(Switch @switch, A arg) => visit(@switch, arg);
        public virtual T visitCase(Case @case, A arg) => visit(@case, arg);

        public virtual T visit(Tree node, A arg) => throw new InvalidOperationException();

        public T scan<TT>(IList<TT> trees, A arg) where TT : Tree
        {
            for (var i = 0; i < trees.Count; i++) {
                scan(trees[i], arg);
            }
            return default;
        }

        public T scan(Tree tree, A arg)
        {
            if (tree != null) {
                return tree.accept(this, arg);
            }
            return default;
        }
    }

    public abstract class AstVisitor
    {
        public virtual void visitCompilationUnit(CompilationUnit compilationUnit) => visit(compilationUnit);
        public virtual void visitStructDef(StructDef structDef) => visit(structDef);
        public virtual void visitFuncDef(FuncDef func) => visit(func);
        public virtual void visitVarDef(VariableDeclaration varDef) => visit(varDef);
        public virtual void visitBinary(BinaryExpressionNode expr) => visit(expr);
        public virtual void visitUnary(UnaryExpressionNode expr) => visit(expr);
        public virtual void visitAssign(AssignNode expr) => visit(expr);
        public virtual void visitCompoundAssign(CompoundAssignNode expr) => visit(expr);
        public virtual void visitLiteral(LiteralExpression literal) => visit(literal);
        public virtual void visitIdent(Identifier ident) => visit(ident);
        public virtual void visitSelect(Select select) => visit(select);
        public virtual void visitIndex(ArrayIndex index) => visit(index);
        public virtual void visitNewStruct(NewStruct newStruct) => visit(newStruct);
        public virtual void visitNewArray(NewArray newArray) => visit(newArray);
        public virtual void visitFuncInvoke(FuncInvocation funcInvocation) => visit(funcInvocation);
        public virtual void visitReturn(ReturnStatement returnStatement) => visit(returnStatement);
        public virtual void visitBlock(Block block) => visit(block);
        public virtual void visitBreak(Break @break) => visit(@break);
        public virtual void visitIf(If @if) => visit(@if);
        public virtual void visitContinue(Continue @continue) => visit(@continue);
        public virtual void visitWhile(WhileStatement whileStatement) => visit(whileStatement);
        public virtual void visitFor(ForLoop forLoop) => visit(forLoop);
        public virtual void visitExpresionStmt(ExpressionStatement expr) => visit(expr);
        public virtual void visitDo(DoStatement doStatement) => visit(doStatement);
        public virtual void visitConditional(ConditionalExpression conditional) => visit(conditional);
        public virtual void visitPrimitiveType(PrimitiveTypeNode prim) => visit(prim);
        public virtual void visitDeclaredType(DeclaredType declaredType) => visit(declaredType);
        public virtual void visitArrayType(ArrayTypeTree arrayType) => visit(arrayType);
        public virtual void visitSwitch(Switch @switch) => visit(@switch);
        public virtual void visitCase(Case @case) => visit(@case);

        public virtual void visit(Tree node) => throw new InvalidOperationException();

        public void scan<T>(IList<T> trees) where T : Tree
        {
            for (var i = 0; i < trees.Count; i++) {
                scan(trees[i]);
            }
        }

        public void scan(Tree tree) => tree?.accept(this);
    }
}
