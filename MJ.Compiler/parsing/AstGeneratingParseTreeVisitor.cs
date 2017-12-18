using System;
using System.Collections.Generic;
using System.Linq;

using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Tree;

using mj.compiler.main;
using mj.compiler.resources;
using mj.compiler.symbol;
using mj.compiler.tree;
using mj.compiler.utils;

using static mj.compiler.parsing.MJParser;

namespace mj.compiler.parsing
{
    public class AstGeneratingParseTreeVisitor : MJBaseVisitor<Tree>
    {
        private readonly Log log;

        public AstGeneratingParseTreeVisitor(Log log)
        {
            this.log = log;
        }

        public override Tree VisitChildren(IRuleNode node) => throw new InvalidOperationException();
        public override Tree VisitErrorNode(IErrorNode node) => throw new InvalidOperationException();

        public override Tree VisitType(TypeContext context)
        {
            TypeTag type;
            IToken token = context.primitive;
            switch (token.Type) {
                case INT:
                    type = TypeTag.INT;
                    break;
                case LONG:
                    type = TypeTag.LONG;
                    break;
                case FLOAT:
                    type = TypeTag.FLOAT;
                    break;
                case DOUBLE:
                    type = TypeTag.DOUBLE;
                    break;
                case BOOLEAN:
                    type = TypeTag.BOOLEAN;
                    break;
                default:
                    throw new ArgumentException();
            }

            int endCol = token.Column + token.StopIndex - token.StopIndex;
            return new PrimitiveTypeNode(token.Line, token.Column, token.Line, endCol, type);
        }

        public override Tree VisitNameExpression(NameExpressionContext context)
        {
            IToken symbol = context.Identifier().Symbol;
            String identifier = symbol.Text;

            int stopColumn = symbol.Column + symbol.StopIndex - symbol.StopIndex;

            return new Identifier(symbol.Line, symbol.Column, symbol.Line, stopColumn, identifier);
        }

        public override Tree VisitCompilationUnit(CompilationUnitContext context)
        {
            IList<MethodDeclarationContext> methodDeclarationContexts = context._methods;
            MethodDef[] methodDefs = new MethodDef[methodDeclarationContexts.Count];

            for (var i = 0; i < methodDeclarationContexts.Count; i++) {
                methodDefs[i] = (MethodDef)VisitMethodDeclaration(methodDeclarationContexts[i]);
            }

            if (methodDefs.Length > 0) {
                int beginLine = methodDefs[0].beginLine;
                int beginCol = methodDefs[0].beginCol;
                int endLine = methodDefs[methodDefs.Length - 1].endLine;
                int endCol = methodDefs[methodDefs.Length - 1].endCol;
                return new CompilationUnit(beginLine, beginCol, endLine, endCol, methodDefs);
            }
            return new CompilationUnit(0, 0, 0, 0, methodDefs);
        }

        public override Tree VisitMethodDeclaration(MethodDeclarationContext context)
        {
            String name = context.name.Text;
            ResultContext resultContext = context.result();
            IList<FormalParameterContext> parameterContexts = context._params;
            bool isPrivate = context.isPrivate;

            TypeContext resType = resultContext.type();
            TypeTree type;
            if (resType != null) {
                type = (TypeTree)VisitType(resType);
            } else {
                //void
                IToken voidToken = resultContext.Start;
                int len = voidToken.StopIndex - voidToken.StartIndex;
                type = new PrimitiveTypeNode(voidToken.Line, voidToken.Column, voidToken.Line, voidToken.Column + len,
                    TypeTag.VOID);
            }

            VariableDeclaration[] parameters = new VariableDeclaration[parameterContexts.Count];
            for (int i = 0; i < parameterContexts.Count; i++) {
                parameters[i] = (VariableDeclaration)VisitFormalParameter(parameterContexts[i]);
            }

            Block block = (Block)VisitBlock(context.methodBody().block());

            return new MethodDef(type.beginLine, type.beginCol, block.endLine, block.endCol, name, type, parameters,
                block, isPrivate);
        }

        public override Tree VisitResult(ResultContext context) => throw new InvalidOperationException();

        public override Tree VisitFormalParameter(FormalParameterContext context)
        {
            String name = context.name.Text;
            TypeTree type = (TypeTree)VisitType(context.type());

            IToken stopToken = context.Stop;
            int stopLine = stopToken.Line;
            int stopCol = stopToken.Column + (stopToken.StopIndex - stopToken.StartIndex);

            return new VariableDeclaration(type.beginLine, type.beginCol, stopLine, stopCol, name, type, null);
        }

        public override Tree VisitMethodBody(MethodBodyContext context) => throw new InvalidOperationException();

        public override Tree VisitBlock(BlockContext context)
        {
            IList<StatementNode> statements = convertBlockStatementList(context.blockStatementList());

            return new Block(context.Start.Line, context.Start.Column,
                context.stop.Line, context.stop.Column, statements);
        }

        public override Tree VisitBlockStatementList(BlockStatementListContext context) =>
            throw new InvalidOperationException();

        public override Tree VisitStatementInBlock(StatementInBlockContext context) =>
            throw new InvalidOperationException();

        public override Tree VisitLocalVariableDeclaration(LocalVariableDeclarationContext context)
        {
            VariableDeclaration localVariableDeclaration =
                (VariableDeclaration)VisitVariableDeclaration(context.variableDeclaration());

            localVariableDeclaration.endLine = context.Stop.Line;
            localVariableDeclaration.endCol = context.Stop.Column;

            return localVariableDeclaration;
        }

        public override Tree VisitVariableDeclaration(VariableDeclarationContext context)
        {
            String name = context.name.Text;
            TypeTree type = (TypeTree)VisitType(context.type());

            ExpressionContext initContext = context.init;
            Expression init = null;
            if (initContext != null) {
                init = (Expression)VisitExpression(initContext);
            }

            return new VariableDeclaration(type.beginLine, type.beginCol, context.Stop.Line,
                context.Stop.Column, name, type, init);
        }

        public override Tree VisitStatement(StatementContext context)
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

        public override Tree VisitStatementNoShortIf(StatementNoShortIfContext context)
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

        public override Tree VisitStatementWithoutTrailingSubstatement(
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

        public override Tree VisitExpressionStatement(ExpressionStatementContext context)
        {
            Expression expression = (Expression)VisitStatementExpression(context.statementExpression());

            return new ExpressionStatement(expression.beginLine, expression.beginCol,
                context.Stop.Line, context.Stop.Column, expression);
        }

        public override Tree VisitStatementExpression(StatementExpressionContext context)
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

        public override Tree VisitIfThenStatement(IfThenStatementContext context)
        {
            Expression condition = (Expression)VisitExpression(context.condition);
            StatementNode ifTrue = (StatementNode)VisitStatement(context.ifTrue);

            return new If(context.Start.Line, context.Start.Column,
                ifTrue.endLine, ifTrue.endCol, condition, ifTrue, elsePart: null);
        }

        public override Tree VisitIfThenElseStatement(IfThenElseStatementContext context)
        {
            Expression condition = (Expression)VisitExpression(context.condition);
            StatementNode ifTrue = (StatementNode)VisitStatementNoShortIf(context.ifTrue);
            StatementNode ifFalse = (StatementNode)VisitStatement(context.ifFalse);

            return new If(context.Start.Line, context.Start.Column,
                ifTrue.endLine, ifTrue.endCol, condition, ifTrue, ifFalse);
        }

        public override Tree VisitIfThenElseStatementNoShortIf(IfThenElseStatementNoShortIfContext context)
        {
            Expression condition = (Expression)VisitExpression(context.condition);
            StatementNode ifTrue = (StatementNode)VisitStatementNoShortIf(context.ifTrue);
            StatementNode ifFalse = (StatementNode)VisitStatementNoShortIf(context.ifFalse);

            return new If(context.Start.Line, context.Start.Column,
                ifTrue.endLine, ifTrue.endCol, condition, ifTrue, ifFalse);
        }

        public override Tree VisitSwitchStatement(SwitchStatementContext context)
        {
            Expression selector = (Expression)VisitExpression(context.expression());
            CaseGroupContext[] caseGroups = context.caseGroup();
            IList<SwitchLabelContext> bottomLabels = context._bottomLabels;

            int count = caseGroups.Select(x => x.labels._labels.Count).Sum();

            Case[] cases = new Case[count + bottomLabels.Count];

            int iCases = 0;
            bool hasDefault = false;
            foreach (CaseGroupContext caseGroup in caseGroups) {
                IList<SwitchLabelContext> labels = caseGroup.labels._labels;
                int last = labels.Count - 1;
                // for each label except last
                for (var index = 0; index < last; index++) {
                    SwitchLabelContext label = labels[index];
                    Expression caseExpression = getCaseExpression(label);
                    checkDefault(label, caseExpression, ref hasDefault);
                    cases[iCases++] = new Case(label.start.Line, label.start.Column, label.stop.Line,
                        label.stop.Column, caseExpression,
                        CollectionUtils.emptyList<StatementNode>());
                }
                SwitchLabelContext lastLabel = labels[last];
                Expression lastCaseExpression = getCaseExpression(lastLabel);
                checkDefault(lastLabel, lastCaseExpression, ref hasDefault);
                IList<StatementNode> statements = convertBlockStatementList(caseGroup.stmts);

                cases[iCases++] = new Case(lastLabel.start.Line, lastLabel.start.Column, caseGroup.stop.Line,
                    caseGroup.stop.Column, lastCaseExpression, statements);
            }

            foreach (SwitchLabelContext label in bottomLabels) {
                Expression caseExpression = getCaseExpression(label);
                checkDefault(label, caseExpression, ref hasDefault);
                cases[iCases++] = new Case(label.start.Line, label.start.Column, label.stop.Line,
                    label.stop.Column, caseExpression,
                    CollectionUtils.emptyList<StatementNode>());
            }

            return new Switch(context.start.Line, context.start.Column, context.stop.Line,
                context.stop.Column, selector, cases, hasDefault);
        }

        private void checkDefault(SwitchLabelContext label, Expression caseExpression, ref bool hasDefault)
        {
            if (caseExpression == null) {
                if (hasDefault) {
                    log.error(
                        new DiagnosticPosition(label.start.Line, label.start.Column),
                        messages.multipleDefaults);
                }
                hasDefault = true;
            }
        }

        private Expression getCaseExpression(SwitchLabelContext label)
        {
            ConstantExpressionContext constExp = label.constantExpression();
            return constExp == null ? null : (Expression)VisitLiteral(constExp.literal());
        }

        private IList<StatementNode> convertBlockStatementList(BlockStatementListContext blockStatementListContext)
        {
            if (blockStatementListContext == null) {
                return CollectionUtils.emptyList<StatementNode>();
            }
            IList<StatementInBlockContext> statementsInBlock = blockStatementListContext._statements;
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

            return statements;
        }

        public override Tree VisitCaseGroup(CaseGroupContext context) => throw new InvalidOperationException();
        public override Tree VisitSwitchLabels(SwitchLabelsContext context) => throw new InvalidOperationException();
        public override Tree VisitSwitchLabel(SwitchLabelContext context) => throw new InvalidOperationException();

        public override Tree VisitConstantExpression(ConstantExpressionContext context) =>
            throw new InvalidOperationException();

        public override Tree VisitWhileStatement(WhileStatementContext context)
        {
            Expression condition = (Expression)VisitExpression(context.condition);
            StatementNode body = (StatementNode)VisitStatement(context.statement());

            return new WhileStatement(context.Start.Line, context.Start.Column,
                body.endLine, body.endCol, condition, body);
        }

        public override Tree VisitWhileStatementNoShortIf(WhileStatementNoShortIfContext context)
        {
            Expression condition = (Expression)VisitExpression(context.condition);
            StatementNode body = (StatementNode)VisitStatementNoShortIf(context.statementNoShortIf());

            return new WhileStatement(context.Start.Line, context.Start.Column,
                body.endLine, body.endCol, condition, body);
        }

        public override Tree VisitDoStatement(DoStatementContext context)
        {
            Expression condition = (Expression)VisitExpression(context.condition);
            StatementNode body = (StatementNode)VisitStatement(context.statement());

            return new DoStatement(context.Start.Line, context.Start.Column,
                body.endLine, body.endCol, condition, body);
        }

        public override Tree VisitForStatement(ForStatementContext context)
        {
            IList<StatementNode> init = GetForInit(context.forInit());
            Expression condition = context.condition == null
                ? null
                : (Expression)VisitExpression(context.condition);
            IList<Expression> update = GetForUpdate(context.update);
            StatementNode body = (StatementNode)VisitStatement(context.statement());

            return new ForLoop(context.Start.Line, context.Start.Column, init, condition, update, body);
        }

        private IList<StatementNode> GetForInit(ForInitContext forInit)
        {
            if (forInit == null) {
                return CollectionUtils.emptyList<StatementNode>();
            }

            StatementExpressionListContext statementExpressionList = forInit.statementExpressionList();
            if (statementExpressionList != null) {
                IList<StatementExpressionContext> statementExpressionContexts = statementExpressionList._statements;
                StatementNode[] statements = new StatementNode[statementExpressionContexts.Count];
                for (var i = 0; i < statementExpressionContexts.Count; i++) {
                    Expression expr = (Expression)VisitStatementExpression(statementExpressionContexts[i]);
                    statements[i] = new ExpressionStatement(expr.beginLine, expr.beginCol,
                        expr.endLine, expr.endCol, expr);
                }
                return statements;
            }
            VariableDeclarationContext variableDeclaration = forInit.variableDeclaration();
            if (variableDeclaration != null) {
                return CollectionUtils.singletonList((StatementNode)VisitVariableDeclaration(variableDeclaration));
            }
            return null;
        }

        private IList<Expression> GetForUpdate(ForUpdateContext forUpdate)
        {
            if (forUpdate == null) {
                return CollectionUtils.emptyList<Expression>();
            }
            StatementExpressionListContext statementExpressionList = forUpdate.statementExpressionList();
            IList<StatementExpressionContext> statementExpressionContexts = statementExpressionList._statements;
            Expression[] expressions = new Expression[statementExpressionContexts.Count];
            for (var i = 0; i < statementExpressionContexts.Count; i++) {
                expressions[i] = (Expression)VisitStatementExpression(statementExpressionContexts[i]);
            }
            return expressions;
        }

        public override Tree VisitForStatementNoShortIf(ForStatementNoShortIfContext context)
        {
            IList<StatementNode> init = GetForInit(context.forInit());
            Expression condition = context.condition == null
                ? null
                : (Expression)VisitExpression(context.condition);
            IList<Expression> update = GetForUpdate(context.update);
            StatementNode body = (StatementNode)VisitStatementNoShortIf(context.statementNoShortIf());

            return new ForLoop(context.Start.Line, context.Start.Column, init, condition, update, body);
        }

        public override Tree VisitForInit(ForInitContext context) => throw new InvalidOperationException();
        public override Tree VisitForUpdate(ForUpdateContext context) => throw new InvalidOperationException();

        public override Tree VisitStatementExpressionList(StatementExpressionListContext context) =>
            throw new InvalidOperationException();

        public override Tree VisitBreakStatement(BreakStatementContext context)
        {
            return new Break(context.Start.Line, context.Start.Column, context.Stop.Line,
                context.Stop.Column);
        }

        public override Tree VisitContinueStatement(ContinueStatementContext context)
        {
            return new Continue(context.Start.Line, context.Start.Column, context.Stop.Line,
                context.Stop.Column);
        }

        public override Tree VisitReturnStatement(ReturnStatementContext context)
        {
            ExpressionContext valueContext = context.value;
            Expression value;

            if (valueContext != null) {
                value = (Expression)VisitExpression(valueContext);
            } else {
                value = null;
            }

            int endLine = context.Stop.Line;
            int endCol = context.Stop.Column;

            return new ReturnStatement(context.Start.Line, context.Start.Column, endLine, endCol, value);
        }

        public override Tree VisitMethodInvocation(MethodInvocationContext context)
        {
            IToken methodName = context.neme;
            ArgumentListContext argumentList = context.argumentList();
            IList<ExpressionContext> parsedArgs = argumentList != null
                ? argumentList._args
                : CollectionUtils.emptyList<ExpressionContext>();

            Expression[] args = new Expression[parsedArgs.Count];

            for (var i = 0; i < parsedArgs.Count; i++) {
                args[i] = (Expression)VisitExpression(parsedArgs[i]);
            }

            IToken stopToken = context.Stop;
            int endLine = stopToken.Line;
            int endCol = stopToken.StopIndex - stopToken.StartIndex + 1;

            return new MethodInvocation(methodName.Line, methodName.Column, endLine, endCol, methodName.Text, args);
        }

        public override Tree VisitArgumentList(ArgumentListContext context) => throw new InvalidOperationException();

        public override Tree VisitAssignment(AssignmentContext context)
        {
            Identifier target = (Identifier)VisitNameExpression(context.leftHandSide().nameExpression());
            Expression expression = (Expression)VisitExpression(context.expression());

            Tag op;

            switch (context.assignmentOperator().op.Type) {
                case ASSIGN:
                    return new AssignNode(target, expression);
                case ADD_ASSIGN:
                    op = Tag.PLUS_ASG;
                    break;
                case SUB_ASSIGN:
                    op = Tag.MINUS_ASG;
                    break;
                case MUL_ASSIGN:
                    op = Tag.MUL_ASG;
                    break;
                case DIV_ASSIGN:
                    op = Tag.DIV_ASG;
                    break;
                case AND_ASSIGN:
                    op = Tag.BITAND_ASG;
                    break;
                case OR_ASSIGN:
                    op = Tag.BITOR_ASG;
                    break;
                case XOR_ASSIGN:
                    op = Tag.BITXOR_ASG;
                    break;
                case MOD_ASSIGN:
                    op = Tag.MOD_ASG;
                    break;
                case LSHIFT_ASSIGN:
                    op = Tag.SHL_ASG;
                    break;
                case RSHIFT_ASSIGN:
                    op = Tag.SHR_ASG;
                    break;
                default:
                    throw new InvalidOperationException();
            }

            return new CompoundAssignNode(op, target, expression);
        }

        public override Tree VisitLeftHandSide(LeftHandSideContext context) => throw new InvalidOperationException();

        public override Tree VisitAssignmentOperator(AssignmentOperatorContext context) =>
            throw new InvalidOperationException();

        public override Tree VisitConditionalExpression(ConditionalExpressionContext context)
        {
            ConditionalOrExpressionContext lower = context.down;
            if (lower != null) {
                return VisitConditionalOrExpression(lower);
            }

            Expression condition = (Expression)VisitConditionalOrExpression(context.condition);
            Expression ifTrue = (Expression)VisitExpression(context.ifTrue);
            Expression ifFalse = (Expression)VisitConditionalExpression(context.ifFalse);

            return new ConditionalExpression(condition, ifTrue, ifFalse);
        }

        public override Tree VisitConditionalOrExpression(ConditionalOrExpressionContext context)
        {
            ConditionalAndExpressionContext lower = context.down;
            if (lower != null) {
                return VisitConditionalAndExpression(lower);
            }

            Expression left = (Expression)VisitConditionalOrExpression(context.left);
            Expression right = (Expression)VisitConditionalAndExpression(context.right);

            return new BinaryExpressionNode(Tag.OR, left, right);
        }

        public override Tree VisitConditionalAndExpression(ConditionalAndExpressionContext context)
        {
            InclusiveOrExpressionContext lower = context.down;
            if (lower != null) {
                return VisitInclusiveOrExpression(lower);
            }

            Expression left = (Expression)VisitConditionalAndExpression(context.left);
            Expression right = (Expression)VisitInclusiveOrExpression(context.right);

            return new BinaryExpressionNode(Tag.AND, left, right);
        }

        public override Tree VisitInclusiveOrExpression(InclusiveOrExpressionContext context)
        {
            ExclusiveOrExpressionContext lower = context.down;
            if (lower != null) {
                return VisitExclusiveOrExpression(lower);
            }

            Expression left = (Expression)VisitInclusiveOrExpression(context.left);
            Expression right = (Expression)VisitExclusiveOrExpression(context.right);

            return new BinaryExpressionNode(Tag.BITOR, left, right);
        }

        public override Tree VisitExclusiveOrExpression(ExclusiveOrExpressionContext context)
        {
            AndExpressionContext lower = context.down;
            if (lower != null) {
                return VisitAndExpression(lower);
            }

            Expression left = (Expression)VisitExclusiveOrExpression(context.left);
            Expression right = (Expression)VisitAndExpression(context.right);

            return new BinaryExpressionNode(Tag.BITXOR, left, right);
        }

        public override Tree VisitAndExpression(AndExpressionContext context)
        {
            EqualityExpressionContext lower = context.down;
            if (lower != null) {
                return VisitEqualityExpression(lower);
            }

            Expression left = (Expression)VisitAndExpression(context.left);
            Expression right = (Expression)VisitEqualityExpression(context.right);

            return new BinaryExpressionNode(Tag.BITAND, left, right);
        }

        public override Tree VisitEqualityExpression(EqualityExpressionContext context)
        {
            RelationalExpressionContext lower = context.down;
            if (lower != null) {
                return VisitRelationalExpression(lower);
            }

            Expression left = (Expression)VisitEqualityExpression(context.left);
            Expression right = (Expression)VisitRelationalExpression(context.right);

            Tag op;
            switch (context.@operator.Type) {
                case EQUAL:
                    op = Tag.EQ;
                    break;
                case NOTEQUAL:
                    op = Tag.NEQ;
                    break;
                default:
                    throw new InvalidOperationException();
            }

            return new BinaryExpressionNode(op, left, right);
        }

        public override Tree VisitRelationalExpression(RelationalExpressionContext context)
        {
            ShiftExpressionContext lower = context.down;
            if (lower != null) {
                return VisitShiftExpression(lower);
            }

            Expression left = (Expression)VisitRelationalExpression(context.left);
            Expression right = (Expression)VisitShiftExpression(context.right);

            Tag op;
            switch (context.@operator.Type) {
                case LT:
                    op = Tag.LT;
                    break;
                case GT:
                    op = Tag.GT;
                    break;
                case LE:
                    op = Tag.LE;
                    break;
                case GE:
                    op = Tag.GE;
                    break;
                default:
                    throw new InvalidOperationException();
            }

            return new BinaryExpressionNode(op, left, right);
        }

        public override Tree VisitShiftExpression(ShiftExpressionContext context)
        {
            AdditiveExpressionContext lower = context.down;
            if (lower != null) {
                return VisitAdditiveExpression(lower);
            }

            Expression left = (Expression)VisitShiftExpression(context.left);
            Expression right = (Expression)VisitAdditiveExpression(context.right);

            Tag op;
            switch (context.@operator) {
                case LSHIFT:
                    op = Tag.SHL;
                    break;
                case RSHIFT:
                    op = Tag.SHR;
                    break;
                default:
                    throw new InvalidOperationException();
            }

            return new BinaryExpressionNode(op, left, right);
        }

        public override Tree VisitPreIncDecExpression(PreIncDecExpressionContext context)
        {
            Tag op = context.@operator.Type == INC ? Tag.PRE_INC : Tag.PRE_DEC;

            Expression arg = (Expression)VisitUnaryExpression(context.arg);
            return new UnaryExpressionNode(context.start.Line, context.start.Column, arg.endLine,
                arg.endCol, op, arg);
        }

        public override Tree VisitUnaryExpressionNotPlusMinus(UnaryExpressionNotPlusMinusContext context)
        {
            PostfixExpressionContext lower = context.down;
            if (lower != null) {
                return VisitPostfixExpression(lower);
            }

            Tag op = context.@operator.Type == TILDE ? Tag.COMPL : Tag.NOT;
            Expression arg = (Expression)VisitUnaryExpression(context.arg);
            return new UnaryExpressionNode(context.start.Line, context.start.Column, arg.endLine,
                arg.endCol, op, arg);
        }

        public override Tree VisitPostfixExpression(PostfixExpressionContext context)
        {
            Expression current;
            NameExpressionContext name = context.nameExpression();
            if (name != null) {
                current = (Expression)VisitNameExpression(name);
            } else {
                current = (Expression)VisitPrimary(context.down);
            }

            for (int i = 0, count = context._postfixes.Count; i < count; i++) {
                IToken postfix = context._postfixes[i];
                Tag op;
                switch (postfix.Type) {
                    case INC:
                        op = Tag.POST_INC;
                        break;
                    case DEC:
                        op = Tag.POST_DEC;
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

        public override Tree VisitPostIncDecExpression(PostIncDecExpressionContext context)
        {
            Tag op = context.@operator.Type == INC ? Tag.POST_INC : Tag.POST_DEC;

            Expression arg = (Expression)VisitPostfixExpression(context.arg);
            return new UnaryExpressionNode(arg.beginLine, arg.beginCol, arg.endLine,
                arg.endCol, op, arg);
        }

        public override Tree VisitExpression(ExpressionContext context)
        {
            var conditional = context.conditionalExpression();
            return conditional != null
                ? VisitConditionalExpression(conditional)
                : VisitAssignment(context.assignment());
        }

        public override Tree VisitPrimary(PrimaryContext context)
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

        public override Tree VisitLiteral(LiteralContext context)
        {
            IToken symbol;
            String text;
            Object value;
            TypeTag type;

            switch (context.literalType) {
                case INT:
                    symbol = context.IntegerLiteral().Symbol;
                    text = symbol.Text;
                    if (Int32.TryParse(text, out var intLit)) {
                        type = TypeTag.INT;
                        value = intLit;
                    } else if (Int64.TryParse(text, out var longLit)) {
                        type = TypeTag.LONG;
                        value = longLit;
                    } else {
                        throw new ArgumentOutOfRangeException("Integer literal too big");
                    }
                    break;
                case FLOAT:
                    symbol = context.FloatingPointLiteral().Symbol;
                    text = symbol.Text;
                    if (Single.TryParse(text, out var floatLit)) {
                        type = TypeTag.FLOAT;
                        value = floatLit;
                    } else if (Double.TryParse(text, out var doubleLit)) {
                        type = TypeTag.DOUBLE;
                        value = doubleLit;
                    } else {
                        throw new ArgumentOutOfRangeException("Floating point literal too big");
                    }
                    break;
                case BOOLEAN:
                    symbol = context.BooleanLiteral().Symbol;
                    text = symbol.Text;
                    type = TypeTag.BOOLEAN;
                    value = Boolean.Parse(text);
                    break;
                default:
                    throw new InvalidOperationException();
            }

            int line = symbol.Line;
            int startColumn = symbol.Column;
            int endColumn = startColumn + text.Length;

            return new LiteralExpression(line, startColumn, line, endColumn, type, value);
        }

        public override Tree VisitAdditiveExpression(AdditiveExpressionContext context)
        {
            MultiplicativeExpressionContext lower = context.down;
            if (lower != null) {
                return VisitMultiplicativeExpression(lower);
            }

            Expression left = (Expression)VisitAdditiveExpression(context.left);
            Expression right = (Expression)VisitMultiplicativeExpression(context.right);

            Tag op;

            switch (context.@operator.Type) {
                case ADD:
                    op = Tag.PLUS;
                    break;
                case SUB:
                    op = Tag.MINUS;
                    break;
                default: throw new InvalidOperationException();
            }

            return new BinaryExpressionNode(op, left, right);
        }

        public override Tree VisitMultiplicativeExpression(MultiplicativeExpressionContext context)
        {
            UnaryExpressionContext lower = context.down;
            if (lower != null) {
                return VisitUnaryExpression(lower);
            }

            Expression left = (Expression)VisitMultiplicativeExpression(context.left);
            Expression right = (Expression)VisitUnaryExpression(context.right);

            Tag op;

            switch (context.@operator.Type) {
                case MUL:
                    op = Tag.MUL;
                    break;
                case DIV:
                    op = Tag.DIV;
                    break;
                case MOD:
                    op = Tag.MOD;
                    break;
                default: throw new InvalidOperationException();
            }

            return new BinaryExpressionNode(op, left, right);
        }

        public override Tree VisitUnaryExpression(UnaryExpressionContext context)
        {
            UnaryExpressionNotPlusMinusContext lower = context.down;
            if (lower != null) {
                return VisitUnaryExpressionNotPlusMinus(lower);
            }

            Tag op;

            switch (context.@operator.Type) {
                case INC:
                    op = Tag.PRE_INC;
                    break;
                case DEC:
                    op = Tag.PRE_DEC;
                    break;
                case ADD:
                    op = Tag.NEG;
                    break;
                default: throw new InvalidOperationException();
            }

            Expression arg = (Expression)VisitUnaryExpression(context.arg);

            return new UnaryExpressionNode(context.Start.Line, context.Start.Column, arg.endLine,
                arg.endCol, op, arg);
        }
    }
}
