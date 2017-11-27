using System;
using System.Collections.Generic;
using System.Linq;

using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

using mj.compiler.parsing.ast;
using mj.compiler.utils;

using static mj.compiler.parsing.MJParser;

namespace mj.compiler.parsing
{
    public class AstGeneratingParseTreeVisitor : MJBaseVisitor<AstNode>
    {
        public override AstNode VisitChildren(IRuleNode node) => throw new InvalidOperationException();
        public override AstNode VisitErrorNode(IErrorNode node) => throw new InvalidOperationException();

        public override AstNode VisitType(TypeContext context)
        {
            PrimitiveType type;
            IToken token = context.primitive;
            switch (token.Type) {
                case INT:
                    type = PrimitiveType.INT;
                    break;
                case LONG:
                    type = PrimitiveType.LONG;
                    break;
                case FLOAT:
                    type = PrimitiveType.FLOAT;
                    break;
                case DOUBLE:
                    type = PrimitiveType.DOUBLE;
                    break;
                case BOOLEAN:
                    type = PrimitiveType.BOOLEAN;
                    break;
                default:
                    throw new ArgumentException();
            }

            int endCol = token.Column + token.StopIndex - token.StopIndex;
            return new PrimitiveTypeNode(token.Line, token.Column, token.Line, endCol, type);
        }

        public override AstNode VisitNameExpression(NameExpressionContext context)
        {
            IToken symbol = context.Identifier().Symbol;
            String identifier = symbol.Text;

            int stopColumn = symbol.Column + symbol.StopIndex - symbol.StopIndex;

            return new IdentifierNode(symbol.Line, symbol.Column, symbol.Line, stopColumn, identifier);
        }

        public override AstNode VisitCompilationUnit(CompilationUnitContext context)
        {
            IList<MethodDeclarationContext> methodDeclarationContexts = context._methods;
            MethodNode[] methodNodes = new MethodNode[methodDeclarationContexts.Count];

            for (var i = 0; i < methodDeclarationContexts.Count; i++) {
                methodNodes[i] = (MethodNode)VisitMethodDeclaration(methodDeclarationContexts[i]);
            }

            return new CompilatioUnit(methodNodes);
        }

        public override AstNode VisitMethodDeclaration(MethodDeclarationContext context)
        {
            String name = context.name.Text;
            ResultContext resultContext = context.result();
            IList<FormalParameterContext> parameterContexts = context._params;

            TypeContext resType = resultContext.type();
            TypeRefNode type;
            if (resType != null) {
                type = (TypeRefNode)VisitType(resType);
            } else {
                //void
                IToken voidToken = resultContext.Start;
                int len = voidToken.StopIndex - voidToken.StartIndex;
                type = new PrimitiveTypeNode(voidToken.Line, voidToken.Column, voidToken.Line, voidToken.Column + len,
                    PrimitiveType.VOID);
            }

            MethodParameter[] parameters = new MethodParameter[parameterContexts.Count];
            for (int i = 0; i < parameterContexts.Count; i++) {
                parameters[i] = (MethodParameter)VisitFormalParameter(parameterContexts[i]);
            }

            Block block = (Block)VisitBlock(context.methodBody().block());

            return new MethodNode(type.beginLine, type.beginCol, block.endLine, block.endCol, name, type, parameters,
                block);
        }

        public override AstNode VisitResult(ResultContext context) => throw new InvalidOperationException();

        public override AstNode VisitFormalParameter(FormalParameterContext context)
        {
            String name = context.name.Text;
            TypeRefNode type = (TypeRefNode)VisitType(context.type());

            IToken stopToken = context.Stop;
            int stopLine = stopToken.Line;
            int stopCol = stopToken.Column + (stopToken.StopIndex - stopToken.StartIndex);

            return new MethodParameter(type.beginLine, type.beginCol, stopLine, stopCol, name, type);
        }

        public override AstNode VisitMethodBody(MethodBodyContext context) => throw new InvalidOperationException();

        public override AstNode VisitBlock(BlockContext context)
        {
            IList<StatementInBlockContext> statementsInBlock = context.blockStatementList()._statements;

            StatementNode[] statements = new StatementNode[statementsInBlock.Count];

            for (var i = 0; i < statementsInBlock.Count; i++) {
                StatementContext statementContext = statementsInBlock[i].statement();
                if (statementContext != null) {
                    statements[i] = (StatementNode)VisitStatement(statementContext);
                } else {
                    LocalVariableDeclarationContext localVarCtx =
                        statementsInBlock[i].localVariableDeclaration();
                    statements[i] = (StatementNode)VisitLocalVariableDeclaration(localVarCtx);
                }
            }

            return new Block(context.Start.Line, context.Start.Column,
                context.stop.Line, context.stop.Column, statements);
        }

        public override AstNode VisitBlockStatementList(BlockStatementListContext context) =>
            throw new InvalidOperationException();

        public override AstNode VisitStatementInBlock(StatementInBlockContext context) =>
            throw new InvalidOperationException();

        public override AstNode VisitLocalVariableDeclaration(LocalVariableDeclarationContext context)
        {
            LocalVariableDeclaration localVariableDeclaration =
                (LocalVariableDeclaration)VisitVariableDeclaration(context.variableDeclaration());

            localVariableDeclaration.endLine = context.Stop.Line;
            localVariableDeclaration.endCol = context.Stop.Column;

            return localVariableDeclaration;
        }

        public override AstNode VisitVariableDeclaration(VariableDeclarationContext context)
        {
            String name = context.name.Text;
            TypeRefNode type = (TypeRefNode)VisitType(context.type());

            ExpressionContext init = context.init;
            ExpressionNode initNode = null;
            if (init != null) {
                initNode = (ExpressionNode)VisitExpression(init);
            }

            return new LocalVariableDeclaration(type.beginLine, type.beginCol, context.Stop.Line,
                context.Stop.Column, name, type, initNode);
        }

        public override AstNode VisitStatement(StatementContext context)
        {
            StatementWithoutTrailingSubstatementContext swtss = context
                .statementWithoutTrailingSubstatement();
            if (swtss != null) {
                return VisitStatementWithoutTrailingSubstatement(swtss);
            }
            IfThenStatementContext its = context.ifThenStatement();
            if (its != null) {
                return VisitIfThenStatement(its);
            }
            IfThenElseStatementContext ites = context.ifThenElseStatement();
            if (ites != null) {
                return VisitIfThenElseStatement(ites);
            }
            WhileStatementContext ws = context.whileStatement();
            if (ws != null) {
                return VisitWhileStatement(ws);
            }
            ForStatementContext fs = context.forStatement();
            if (fs != null) {
                return VisitForStatement(fs);
            }
            return null;
        }

        public override AstNode VisitStatementNoShortIf(StatementNoShortIfContext context)
        {
            StatementWithoutTrailingSubstatementContext swtss = context
                .statementWithoutTrailingSubstatement();
            if (swtss != null) {
                return VisitStatementWithoutTrailingSubstatement(swtss);
            }
            IfThenElseStatementNoShortIfContext ites = context.ifThenElseStatementNoShortIf();
            if (ites != null) {
                return VisitIfThenElseStatementNoShortIf(ites);
            }
            WhileStatementNoShortIfContext ws = context.whileStatementNoShortIf();
            if (ws != null) {
                return VisitWhileStatementNoShortIf(ws);
            }
            ForStatementNoShortIfContext fs = context.forStatementNoShortIf();
            if (fs != null) {
                return VisitForStatementNoShortIf(fs);
            }
            return null;
        }

        public override AstNode VisitStatementWithoutTrailingSubstatement(
            StatementWithoutTrailingSubstatementContext context)
        {
            BlockContext block = context.block();
            if (block != null) {
                return VisitBlock(block);
            }
            ExpressionStatementContext expressionStatement = context.expressionStatement();
            if (expressionStatement != null) {
                return VisitExpressionStatement(expressionStatement);
            }
            SwitchStatementContext switchStatement = context.switchStatement();
            if (switchStatement != null) {
                return VisitSwitchStatement(switchStatement);
            }
            DoStatementContext doStatement = context.doStatement();
            if (doStatement != null) {
                return VisitDoStatement(doStatement);
            }
            BreakStatementContext breakStatement = context.breakStatement();
            if (breakStatement != null) {
                return VisitBreakStatement(breakStatement);
            }
            ContinueStatementContext continueStatement = context.continueStatement();
            if (continueStatement != null) {
                return VisitContinueStatement(continueStatement);
            }
            ReturnStatementContext returnStatement = context.returnStatement();
            if (returnStatement != null) {
                return VisitReturnStatement(returnStatement);
            }
            return null;
        }

        public override AstNode VisitExpressionStatement(ExpressionStatementContext context)
        {
            ExpressionNode expression = (ExpressionNode)VisitStatementExpression(context.statementExpression());

            return new ExpressionStatement(expression.beginLine, expression.beginCol,
                context.Stop.Line, context.Stop.Column, expression);
        }

        public override AstNode VisitStatementExpression(StatementExpressionContext context)
        {
            AssignmentContext assignment = context.assignment();
            if (assignment != null) {
                return VisitAssignment(assignment);
            }
            PreIncDecExpressionContext preIncDecExpression = context.preIncDecExpression();
            if (preIncDecExpression != null) {
                return VisitPreIncDecExpression(preIncDecExpression);
            }
            PostIncDecExpressionContext postIncDecExpression = context.postIncDecExpression();
            if (postIncDecExpression != null) {
                return VisitPostIncDecExpression(postIncDecExpression);
            }
            MethodInvocationContext methodInvocation = context.methodInvocation();
            if (methodInvocation != null) {
                return VisitMethodInvocation(methodInvocation);
            }
            return null;
        }

        public override AstNode VisitIfThenStatement(IfThenStatementContext context)
        {
            ExpressionNode condition = (ExpressionNode)VisitExpression(context.condition);
            StatementNode ifTrue = (StatementNode)VisitStatement(context.ifTrue);

            return new If(context.Start.Line, context.Start.Column,
                ifTrue.endLine, ifTrue.endCol, condition, ifTrue, elsePart: null);
        }

        public override AstNode VisitIfThenElseStatement(IfThenElseStatementContext context)
        {
            ExpressionNode condition = (ExpressionNode)VisitExpression(context.condition);
            StatementNode ifTrue = (StatementNode)VisitStatementNoShortIf(context.ifTrue);
            StatementNode ifFalse = (StatementNode)VisitStatement(context.ifFalse);

            return new If(context.Start.Line, context.Start.Column,
                ifTrue.endLine, ifTrue.endCol, condition, ifTrue, ifFalse);
        }

        public override AstNode VisitIfThenElseStatementNoShortIf(IfThenElseStatementNoShortIfContext context)
        {
            ExpressionNode condition = (ExpressionNode)VisitExpression(context.condition);
            StatementNode ifTrue = (StatementNode)VisitStatementNoShortIf(context.ifTrue);
            StatementNode ifFalse = (StatementNode)VisitStatementNoShortIf(context.ifFalse);

            return new If(context.Start.Line, context.Start.Column,
                ifTrue.endLine, ifTrue.endCol, condition, ifTrue, ifFalse);
        }

        public override AstNode VisitSwitchStatement(SwitchStatementContext context)
        {
            ExpressionNode expression = (ExpressionNode)VisitExpression(context.expression());
            CaseGroupContext[] caseGroups = context.caseGroup();
            IList<SwitchLabelContext> bottomLabels = context._bottomLabels;

            int count = caseGroups.Select(x => x.labels._labels.Count).Sum();

            Case[] cases = new Case[count + bottomLabels.Count];

            int iCases = 0;
            foreach (CaseGroupContext caseGroup in caseGroups) {
                IList<SwitchLabelContext> labels = caseGroup.labels._labels;
                int last = labels.Count - 1;
                // for each label except last
                foreach (SwitchLabelContext label in labels) {
                    ExpressionNode caseExpression = (ExpressionNode)VisitLiteral(label.constantExpression().literal());
                    cases[iCases++] = new Case(label.start.Line, label.start.Column, label.stop.Line,
                        label.stop.Column, caseExpression,
                        CollectionUtils.emptyList<StatementNode>());
                }
                SwitchLabelContext lastLabel = labels[last];
                ExpressionNode lastCaseExpression =
                    (ExpressionNode)VisitLiteral(lastLabel.constantExpression().literal());
                StatementNode[] statements = convertStatements(caseGroup.stmts._statements);

                cases[iCases++] = new Case(lastLabel.start.Line, lastLabel.start.Column, caseGroup.stop.Line,
                    caseGroup.stop.Column, lastCaseExpression, statements);
            }

            foreach (SwitchLabelContext label in bottomLabels) {
                ExpressionNode caseExpression = (ExpressionNode)VisitLiteral(label.constantExpression().literal());
                cases[iCases++] = new Case(label.start.Line, label.start.Column, label.stop.Line,
                    label.stop.Column, caseExpression,
                    CollectionUtils.emptyList<StatementNode>());
            }

            return new Switch(context.start.Line, context.start.Column, context.stop.Line,
                context.stop.Column, expression, cases);
        }

        private StatementNode[] convertStatements(IList<StatementInBlockContext> statementContexts)
        {
            StatementNode[] statements = new StatementNode[statementContexts.Count];
            for (var i = 0; i < statementContexts.Count; i++) {
                statements[i] = (StatementNode)VisitStatementInBlock(statementContexts[i]);
            }
            return statements;
        }

        public override AstNode VisitCaseGroup(CaseGroupContext context) => throw new InvalidOperationException();
        public override AstNode VisitSwitchLabels(SwitchLabelsContext context) => throw new InvalidOperationException();
        public override AstNode VisitSwitchLabel(SwitchLabelContext context) => throw new InvalidOperationException();
        public override AstNode VisitConstantExpression(ConstantExpressionContext context) => throw new InvalidOperationException();

        public override AstNode VisitWhileStatement(WhileStatementContext context)
        {
            ExpressionNode condition = (ExpressionNode)VisitExpression(context.condition);
            StatementNode body = (StatementNode)VisitStatement(context.statement());

            return new WhileStatement(context.Start.Line, context.Start.Column,
                body.endLine, body.endCol, condition, body);
        }

        public override AstNode VisitWhileStatementNoShortIf(WhileStatementNoShortIfContext context)
        {
            ExpressionNode condition = (ExpressionNode)VisitExpression(context.condition);
            StatementNode body = (StatementNode)VisitStatementNoShortIf(context.statementNoShortIf());

            return new WhileStatement(context.Start.Line, context.Start.Column,
                body.endLine, body.endCol, condition, body);
        }

        public override AstNode VisitDoStatement(DoStatementContext context)
        {
            ExpressionNode condition = (ExpressionNode)VisitExpression(context.condition);
            StatementNode body = (StatementNode)VisitStatement(context.statement());

            return new DoStatement(context.Start.Line, context.Start.Column,
                body.endLine, body.endCol, condition, body);
        }

        public override AstNode VisitForStatement(ForStatementContext context)
        {
            StatementNode[] init = GetForInit(context.forInit());
            ExpressionNode condition = context.condition == null
                ? null
                : (ExpressionNode)VisitExpression(context.condition);
            ExpressionNode[] update = GetForUpdate(context.update);
            StatementNode body = (StatementNode)VisitStatement(context.statement());

            return new ForLoop(context.Start.Line, context.Start.Column, init, condition, update, body);
        }

        private StatementNode[] GetForInit(ForInitContext forInit)
        {
            StatementExpressionListContext statementExpressionList = forInit.statementExpressionList();
            if (statementExpressionList != null) {
                IList<StatementExpressionContext> statementExpressionContexts = statementExpressionList._statements;
                StatementNode[] statements = new StatementNode[statementExpressionContexts.Count];
                for (var i = 0; i < statementExpressionContexts.Count; i++) {
                    ExpressionNode expr = (ExpressionNode)VisitStatementExpression(statementExpressionContexts[i]);
                    statements[i] = new ExpressionStatement(expr.beginLine, expr.beginCol,
                        expr.endLine, expr.endCol, expr);
                }
                return statements;
            }
            VariableDeclarationContext variableDeclaration = forInit.variableDeclaration();
            if (variableDeclaration != null) {
                return new StatementNode[] {(StatementNode)VisitVariableDeclaration(variableDeclaration)};
            }
            return null;
        }

        private ExpressionNode[] GetForUpdate(ForUpdateContext forUpdate)
        {
            StatementExpressionListContext statementExpressionList = forUpdate.statementExpressionList();
            IList<StatementExpressionContext> statementExpressionContexts = statementExpressionList._statements;
            ExpressionNode[] expressions = new ExpressionNode[statementExpressionContexts.Count];
            for (var i = 0; i < statementExpressionContexts.Count; i++) {
                expressions[i] = (ExpressionNode)VisitStatementExpression(statementExpressionContexts[i]);
            }
            return expressions;
        }

        public override AstNode VisitForStatementNoShortIf(ForStatementNoShortIfContext context)
        {
            StatementNode[] init = GetForInit(context.forInit());
            ExpressionNode condition = context.condition == null
                ? null
                : (ExpressionNode)VisitExpression(context.condition);
            ExpressionNode[] update = GetForUpdate(context.update);
            StatementNode body = (StatementNode)VisitStatementNoShortIf(context.statementNoShortIf());

            return new ForLoop(context.Start.Line, context.Start.Column, init, condition, update, body);
        }

        public override AstNode VisitForInit(ForInitContext context) => throw new InvalidOperationException();
        public override AstNode VisitForUpdate(ForUpdateContext context) => throw new InvalidOperationException();

        public override AstNode VisitStatementExpressionList(StatementExpressionListContext context) =>
            throw new InvalidOperationException();

        public override AstNode VisitBreakStatement(BreakStatementContext context)
        {
            return new Break(context.Start.Line, context.Start.Column, context.Stop.Line,
                context.Stop.Column);
        }

        public override AstNode VisitContinueStatement(ContinueStatementContext context)
        {
            return new Continue(context.Start.Line, context.Start.Column, context.Stop.Line,
                context.Stop.Column);
        }

        public override AstNode VisitReturnStatement(ReturnStatementContext context)
        {
            ExpressionContext valueContext = context.value;
            ExpressionNode value;

            if (valueContext != null) {
                value = (ExpressionNode)VisitExpression(valueContext);
            } else {
                value = null;
            }

            int endLine = context.Stop.Line;
            int endCol = context.Stop.Column;

            return new ReturnStatement(context.Start.Line, context.Start.Column, endLine, endCol, value);
        }

        public override AstNode VisitMethodInvocation(MethodInvocationContext context)
        {
            IToken methodName = context.neme;
            IList<ExpressionContext> parsedArgs = context.argumentList()._args;

            ExpressionNode[] args = new ExpressionNode[parsedArgs.Count];

            for (var i = 0; i < parsedArgs.Count; i++) {
                args[i] = (ExpressionNode)VisitExpression(parsedArgs[i]);
            }

            IToken stopToken = context.Stop;
            int endLine = stopToken.Line;
            int endCol = stopToken.StopIndex - stopToken.StartIndex + 1;

            return new MethodInvocation(methodName.Line, methodName.Column, endLine, endCol, methodName.Text, args);
        }

        public override AstNode VisitArgumentList(ArgumentListContext context) => throw new InvalidOperationException();

        public override AstNode VisitAssignment(AssignmentContext context)
        {
            IdentifierNode target = (IdentifierNode)VisitNameExpression(context.leftHandSide().nameExpression());

            Operator op;

            switch (context.assignmentOperator().op.Type) {
                case ASSIGN:
                    op = Operator.ASSIGN;
                    break;
                case ADD_ASSIGN:
                    op = Operator.ADD_ASSIGN;
                    break;
                case SUB_ASSIGN:
                    op = Operator.SUB_ASSIGN;
                    break;
                case MUL_ASSIGN:
                    op = Operator.MUL_ASSIGN;
                    break;
                case DIV_ASSIGN:
                    op = Operator.DIV_ASSIGN;
                    break;
                case AND_ASSIGN:
                    op = Operator.AND_ASSIGN;
                    break;
                case OR_ASSIGN:
                    op = Operator.OR_ASSIGN;
                    break;
                case XOR_ASSIGN:
                    op = Operator.XOR_ASSIGN;
                    break;
                case MOD_ASSIGN:
                    op = Operator.MOD_ASSIGN;
                    break;
                case LSHIFT_ASSIGN:
                    op = Operator.LSHIFT_ASSIGN;
                    break;
                case RSHIFT_ASSIGN:
                    op = Operator.RSHIFT_ASSIGN;
                    break;
                default:
                    throw new InvalidOperationException();
            }

            ExpressionNode expression = (ExpressionNode)VisitExpression(context.expression());

            return new BinaryExpressionNode(op, target, expression);
        }

        public override AstNode VisitLeftHandSide(LeftHandSideContext context) => throw new InvalidOperationException();

        public override AstNode VisitAssignmentOperator(AssignmentOperatorContext context) =>
            throw new InvalidOperationException();

        public override AstNode VisitConditionalExpression(ConditionalExpressionContext context)
        {
            ConditionalOrExpressionContext lower = context.down;
            if (lower != null) {
                return VisitConditionalOrExpression(lower);
            }

            ExpressionNode condition = (ExpressionNode)VisitConditionalOrExpression(context.condition);
            ExpressionNode ifTrue = (ExpressionNode)VisitExpression(context.ifTrue);
            ExpressionNode ifFalse = (ExpressionNode)VisitConditionalExpression(context.ifFalse);

            return new ConditionalExpressionNode(condition, ifTrue, ifFalse);
        }

        public override AstNode VisitConditionalOrExpression(ConditionalOrExpressionContext context)
        {
            ConditionalAndExpressionContext lower = context.down;
            if (lower != null) {
                return VisitConditionalAndExpression(lower);
            }

            ExpressionNode left = (ExpressionNode)VisitConditionalOrExpression(context.left);
            ExpressionNode right = (ExpressionNode)VisitConditionalAndExpression(context.right);

            return new BinaryExpressionNode(Operator.OR, left, right);
        }

        public override AstNode VisitConditionalAndExpression(ConditionalAndExpressionContext context)
        {
            InclusiveOrExpressionContext lower = context.down;
            if (lower != null) {
                return VisitInclusiveOrExpression(lower);
            }

            ExpressionNode left = (ExpressionNode)VisitConditionalAndExpression(context.left);
            ExpressionNode right = (ExpressionNode)VisitInclusiveOrExpression(context.right);

            return new BinaryExpressionNode(Operator.AND, left, right);
        }

        public override AstNode VisitInclusiveOrExpression(InclusiveOrExpressionContext context)
        {
            ExclusiveOrExpressionContext lower = context.down;
            if (lower != null) {
                return VisitExclusiveOrExpression(lower);
            }

            ExpressionNode left = (ExpressionNode)VisitInclusiveOrExpression(context.left);
            ExpressionNode right = (ExpressionNode)VisitExclusiveOrExpression(context.right);

            return new BinaryExpressionNode(Operator.BITOR, left, right);
        }

        public override AstNode VisitExclusiveOrExpression(ExclusiveOrExpressionContext context)
        {
            AndExpressionContext lower = context.down;
            if (lower != null) {
                return VisitAndExpression(lower);
            }

            ExpressionNode left = (ExpressionNode)VisitExclusiveOrExpression(context.left);
            ExpressionNode right = (ExpressionNode)VisitAndExpression(context.right);

            return new BinaryExpressionNode(Operator.XOR, left, right);
        }

        public override AstNode VisitAndExpression(AndExpressionContext context)
        {
            EqualityExpressionContext lower = context.down;
            if (lower != null) {
                return VisitEqualityExpression(lower);
            }

            ExpressionNode left = (ExpressionNode)VisitAndExpression(context.left);
            ExpressionNode right = (ExpressionNode)VisitEqualityExpression(context.right);

            return new BinaryExpressionNode(Operator.BITAND, left, right);
        }

        public override AstNode VisitEqualityExpression(EqualityExpressionContext context)
        {
            RelationalExpressionContext lower = context.down;
            if (lower != null) {
                return VisitRelationalExpression(lower);
            }

            ExpressionNode left = (ExpressionNode)VisitEqualityExpression(context.left);
            ExpressionNode right = (ExpressionNode)VisitRelationalExpression(context.right);

            Operator op;
            switch (context.@operator.Type) {
                case EQUAL:
                    op = Operator.EQ;
                    break;
                case NOTEQUAL:
                    op = Operator.NEQ;
                    break;
                default:
                    throw new InvalidOperationException();
            }

            return new BinaryExpressionNode(op, left, right);
        }

        public override AstNode VisitRelationalExpression(RelationalExpressionContext context)
        {
            ShiftExpressionContext lower = context.down;
            if (lower != null) {
                return VisitShiftExpression(lower);
            }

            ExpressionNode left = (ExpressionNode)VisitRelationalExpression(context.left);
            ExpressionNode right = (ExpressionNode)VisitShiftExpression(context.right);

            Operator op;
            switch (context.@operator.Type) {
                case LT:
                    op = Operator.LT;
                    break;
                case GT:
                    op = Operator.GT;
                    break;
                case LE:
                    op = Operator.LE;
                    break;
                case GE:
                    op = Operator.GE;
                    break;
                default:
                    throw new InvalidOperationException();
            }

            return new BinaryExpressionNode(op, left, right);
        }

        public override AstNode VisitShiftExpression(ShiftExpressionContext context)
        {
            AdditiveExpressionContext lower = context.down;
            if (lower != null) {
                return VisitAdditiveExpression(lower);
            }

            ExpressionNode left = (ExpressionNode)VisitShiftExpression(context.left);
            ExpressionNode right = (ExpressionNode)VisitAdditiveExpression(context.right);

            Operator op;
            switch (context.@operator) {
                case LSHIFT:
                    op = Operator.LSHIFT;
                    break;
                case RSHIFT:
                    op = Operator.RSHIFT;
                    break;
                default:
                    throw new InvalidOperationException();
            }

            return new BinaryExpressionNode(op, left, right);
        }

        public override AstNode VisitPreIncDecExpression(PreIncDecExpressionContext context)
        {
            Operator op = context.@operator.Type == INC ? Operator.INC : Operator.DEC;

            ExpressionNode arg = (ExpressionNode)VisitUnaryExpression(context.arg);
            return new UnaryExpressionNode(context.start.Line, context.start.Column, arg.endLine,
                arg.endCol, op, arg);
        }

        public override AstNode VisitUnaryExpressionNotPlusMinus(UnaryExpressionNotPlusMinusContext context)
        {
            PostfixExpressionContext lower = context.down;
            if (lower != null) {
                return VisitPostfixExpression(lower);
            }

            Operator op = context.@operator.Type == TILDE ? Operator.TILDE : Operator.BANG;
            ExpressionNode arg = (ExpressionNode)VisitUnaryExpression(context.arg);
            return new UnaryExpressionNode(context.start.Line, context.start.Column, arg.endLine,
                arg.endCol, op, arg);
        }

        public override AstNode VisitPostfixExpression(PostfixExpressionContext context)
        {
            ExpressionNode current;
            NameExpressionContext name = context.nameExpression();
            if (name != null) {
                current = (ExpressionNode)VisitNameExpression(name);
            } else {
                current = (ExpressionNode)VisitPrimary(context.down);
            }

            for (var i = 0; i < context._postfixes.Count; i++) {
                IToken postfix = context._postfixes[i];
                Operator op;
                switch (postfix.Type) {
                    case INC:
                        op = Operator.POST_INC;
                        break;
                    case DEC:
                        op = Operator.POST_DEC;
                        break;
                    default:
                        throw new InvalidOperationException();
                }

                int postfixLine = postfix.Line;
                int len = postfix.StopIndex - postfix.StartIndex + 1;
                int endColumn = postfix.Column + len;

                current = new UnaryExpressionNode(current.beginLine, current.beginCol,
                    postfixLine, endColumn, op, current);
            }

            return current;
        }

        public override AstNode VisitPostIncDecExpression(PostIncDecExpressionContext context)
        {
            Operator op = context.@operator.Type == INC ? Operator.POST_INC : Operator.POST_DEC;

            ExpressionNode arg = (ExpressionNode)VisitPostfixExpression(context.arg);
            return new UnaryExpressionNode(arg.beginLine, arg.beginCol, arg.endLine,
                arg.endCol, op, arg);
        }

        public override AstNode VisitExpression(ExpressionContext context)
        {
            var conditional = context.conditionalExpression();
            return conditional != null
                ? VisitConditionalExpression(conditional)
                : VisitAssignment(context.assignment());
        }

        public override AstNode VisitPrimary(PrimaryContext context)
        {
            var expressionContext = context.parenthesized;
            if (expressionContext != null) {
                return VisitExpression(expressionContext);
            }

            var methodInvocationContext = context.methodInvocation();
            if (methodInvocationContext != null) {
                return VisitMethodInvocation(methodInvocationContext);
            }

            return VisitLiteral(context.literal());
        }

        public override AstNode VisitLiteral(LiteralContext context)
        {
            IToken symbol;
            String text;
            Object value;
            LiteralType type;

            switch (context.literalType) {
                case INT:
                    symbol = context.IntegerLiteral().Symbol;
                    text = symbol.Text;
                    if (Int32.TryParse(text, out var intLit)) {
                        type = LiteralType.INT;
                        value = intLit;
                    } else if (Int64.TryParse(text, out var longLit)) {
                        type = LiteralType.LONG;
                        value = longLit;
                    } else {
                        throw new ArgumentOutOfRangeException("Integer literal too big");
                    }
                    break;
                case FLOAT:
                    symbol = context.FloatingPointLiteral().Symbol;
                    text = symbol.Text;
                    if (Single.TryParse(text, out var floatLit)) {
                        type = LiteralType.FLOAT;
                        value = floatLit;
                    } else if (Double.TryParse(text, out var doubleLit)) {
                        type = LiteralType.DOUBLE;
                        value = doubleLit;
                    } else {
                        throw new ArgumentOutOfRangeException("Floating point literal too big");
                    }
                    break;
                case BOOLEAN:
                    symbol = context.BooleanLiteral().Symbol;
                    text = symbol.Text;
                    type = LiteralType.BOOLEAN;
                    value = Boolean.Parse(text);
                    break;
                default:
                    throw new InvalidOperationException();
            }

            int line = symbol.Line;
            int startColumn = symbol.Column;
            int endColumn = startColumn + text.Length;

            return new LiteralExpressionNode(line, startColumn, line, endColumn, type, value);
        }

        public override AstNode VisitAdditiveExpression(AdditiveExpressionContext context)
        {
            MultiplicativeExpressionContext lower = context.down;
            if (lower != null) {
                return VisitMultiplicativeExpression(lower);
            }

            ExpressionNode left = (ExpressionNode)VisitAdditiveExpression(context.left);
            ExpressionNode right = (ExpressionNode)VisitMultiplicativeExpression(context.right);

            Operator op;

            switch (context.@operator.Type) {
                case ADD:
                    op = Operator.ADD;
                    break;
                case SUB:
                    op = Operator.SUB;
                    break;
                default: throw new InvalidOperationException();
            }

            return new BinaryExpressionNode(op, left, right);
        }

        public override AstNode VisitMultiplicativeExpression(MultiplicativeExpressionContext context)
        {
            UnaryExpressionContext lower = context.down;
            if (lower != null) {
                return VisitUnaryExpression(lower);
            }

            ExpressionNode left = (ExpressionNode)VisitMultiplicativeExpression(context.left);
            ExpressionNode right = (ExpressionNode)VisitUnaryExpression(context.right);

            Operator op;

            switch (context.@operator.Type) {
                case MUL:
                    op = Operator.MUL;
                    break;
                case DIV:
                    op = Operator.DIV;
                    break;
                case MOD:
                    op = Operator.MOD;
                    break;
                default: throw new InvalidOperationException();
            }

            return new BinaryExpressionNode(op, left, right);
        }

        public override AstNode VisitUnaryExpression(UnaryExpressionContext context)
        {
            UnaryExpressionNotPlusMinusContext lower = context.down;
            if (lower != null) {
                return VisitUnaryExpressionNotPlusMinus(lower);
            }

            Operator op = context.@operator.Type == ADD ? Operator.ADD : Operator.SUB;

            ExpressionNode arg = (ExpressionNode)VisitUnaryExpression(context.arg);

            return new UnaryExpressionNode(context.Start.Line, context.Start.Column, arg.endLine,
                arg.endCol, op, arg);
        }
    }
}
