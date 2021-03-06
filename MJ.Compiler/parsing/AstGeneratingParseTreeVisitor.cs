﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Antlr4.Runtime;
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
            TypeTree tree;
            IToken token = context.primitive;
            if (token != null) {
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
                    case CHAR:
                        type = TypeTag.CHAR;
                        break;
                    case CSTRING:
                        type = TypeTag.C_STRING;
                        break;
                    default:
                        throw new ArgumentException();
                }

                int endCol = token.Column + token.StopIndex - token.StopIndex;
                tree = new PrimitiveTypeNode(token.Line, token.Column, token.Line, endCol, type);
            } else {
                tree = new DeclaredType(context.Start.Line, context.Start.Column, context.Stop.Line,
                    context.Stop.Column, context.structName.Text);
            }

            for (int i = 0; i < context.arrays; i++) {
                tree = new ArrayTypeTree(context.Start.Line, context.Start.Column, context.Stop.Line,
                    context.Stop.Column, tree);
            }

            return tree;
        }

        public override Tree VisitCompilationUnit(CompilationUnitContext context)
        {
            IList<DeclarationContext> declarationContexts = context._declarations;

            if (declarationContexts.Count > 0) {
                Tree[] decls = new Tree[declarationContexts.Count];
                for (int i = 0; i < declarationContexts.Count; i++) {
                    decls[i] = VisitDeclaration(declarationContexts[i]);
                }

                int beginLine = decls[0].beginLine;
                int beginCol = decls[0].beginCol;
                int endLine = decls[decls.Length - 1].endLine;
                int endCol = decls[decls.Length - 1].endCol;

                return new CompilationUnit(beginLine, beginCol, endLine, endCol, decls);
            }
            return new CompilationUnit(0, 0, 0, 0, CollectionUtils.emptyList<Tree>());
        }

        public override Tree VisitDeclaration(DeclarationContext context)
        {
            FuncDeclarationContext func = context.funcDeclaration();
            if (func != null) {
                return VisitFuncDeclaration(func);
            }
            StructDefContext sd = context.structDef();
            if (sd != null) {
                return VisitStructDef(sd);
            }
            throw new ArgumentOutOfRangeException();
        }

        public override Tree VisitStructDef(StructDefContext context)
        {
            String name = context.name.Text;

            Tree[] members = new Tree[context._members.Count];
            for (var i = 0; i < context._members.Count; i++) {
                members[i] = VisitMemberDef(context._members[i]);
            }

            return new StructDef(context.Start.Line, context.Start.Column, context.Stop.Line,
                context.Stop.Column, name, members);
        }

        public override Tree VisitMemberDef(MemberDefContext context)
        {
            FieldDefContext fieldDef = context.fieldDef();
            if (fieldDef != null) {
                return VisitFieldDef(fieldDef);
            }
            FuncDeclarationContext funcDeclaration = context.funcDeclaration();
            return VisitFuncDeclaration(funcDeclaration);
        }

        public override Tree VisitFieldDef(FieldDefContext context)
        {
            String name = context.name.Text;
            TypeTree type = (TypeTree)VisitType(context.type());

            return new VariableDeclaration(context.Start.Line, context.Start.Column, context.Stop.Line,
                context.Stop.Column, name, type, null);
        }

        public override Tree VisitFuncDeclaration(FuncDeclarationContext context)
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

            Block block = (Block)VisitBlock(context.funcBody().block());

            return new FuncDef(type.beginLine, type.beginCol, block.endLine, block.endCol, name, type, parameters,
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

        public override Tree VisitFuncBody(FuncBodyContext context) => throw new InvalidOperationException();

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
            IToken token = context.token;
            if (token != null) {
                switch (token.Type) {
                    case LBRACE: return makeBlock(context);
                    case IF: return makeIf(context);
                    case FOR: return makeFor(context);
                    case WHILE: return makeWhile(context);
                    case DO: return makeDo(context);
                    case SWITCH: return makeSwitch(context);
                    case RETURN: return makeReturn(context);
                    case BREAK: return makeBreak(context);
                    case CONTINUE: return makeContinue(context);
                }
            } else {
                return makeExpressionStatement(context);
            }

            return null;
        }

        private Block makeBlock(StatementContext stat)
        {
            IList<StatementNode> statements = convertBlockStatementList(stat.blockStatementList());

            return new Block(stat.Start.Line, stat.Start.Column,
                stat.stop.Line, stat.stop.Column, statements);
        }

        private If makeIf(StatementContext stat)
        {
            Expression condition = (Expression)VisitExpression(stat.expression());
            StatementNode ifTrue = (StatementNode)VisitStatement(stat.ifTrue);
            StatementNode ifFalse = stat.ifFalse == null
                ? null
                : (StatementNode)VisitStatement(stat.ifFalse);

            if (ifTrue.Tag == Tag.VAR_DEF) {
                log.error(ifTrue.Pos, messages.valDefStatementBody_an, "if");
            }

            if (ifFalse?.Tag == Tag.VAR_DEF) {
                log.error(ifTrue.Pos, messages.valDefStatementBody_an, "else");
            }
            
            return new If(stat.Start.Line, stat.Start.Column,
                ifTrue.endLine, ifTrue.endCol, condition, ifTrue, ifFalse);
        }

        private ForLoop makeFor(StatementContext stat)
        {
            IList<StatementNode> init = getForInit(stat.forInit());
            Expression condition = stat.condition == null
                ? null
                : (Expression)VisitExpression(stat.condition);
            IList<Expression> update = getForUpdate(stat.update);
            StatementNode body = (StatementNode)VisitStatement(stat.body);

            if (body.Tag == Tag.VAR_DEF) {
                log.error(body.Pos, messages.valDefStatementBody_a, "for");
            }

            return new ForLoop(stat.Start.Line, stat.Start.Column, init, condition, update, body);
        }

        private WhileStatement makeWhile(StatementContext stat)
        {
            Expression condition = (Expression)VisitExpression(stat.expression());
            StatementNode body = (StatementNode)VisitStatement(stat.body);
            
            if (body.Tag == Tag.VAR_DEF) {
                log.error(body.Pos, messages.valDefStatementBody_a, "while");
            }

            return new WhileStatement(stat.Start.Line, stat.Start.Column,
                body.endLine, body.endCol, condition, body);
        }

        private DoStatement makeDo(StatementContext stat)
        {
            Expression condition = (Expression)VisitExpression(stat.condition);
            StatementNode body = (StatementNode)VisitStatement(stat.body);

            if (body.Tag == Tag.VAR_DEF) {
                log.error(body.Pos, messages.valDefStatementBody_a, "do");
            }
            
            return new DoStatement(stat.Start.Line, stat.Start.Column,
                body.endLine, body.endCol, condition, body);
        }

        private Switch makeSwitch(StatementContext stat)
        {
            Expression selector = (Expression)VisitExpression(stat.expression());
            CaseGroupContext[] caseGroups = stat.caseGroup();
            IList<SwitchLabelContext> bottomLabels = stat._bottomLabels;

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

            return new Switch(stat.start.Line, stat.start.Column, stat.stop.Line,
                stat.stop.Column, selector, cases, hasDefault);
        }

        private ReturnStatement makeReturn(StatementContext stat)
        {
            ExpressionContext valueContext = stat.expression();
            Expression value = valueContext == null ? null : (Expression)VisitExpression(valueContext);

            return new ReturnStatement(stat.Start.Line, stat.Start.Column,
                stat.Stop.Line, stat.Stop.Column, value);
        }

        private Break makeBreak(StatementContext stat)
        {
            return new Break(stat.Start.Line, stat.Start.Column, stat.Stop.Line, stat.Stop.Column);
        }

        private Continue makeContinue(StatementContext stat)
        {
            return new Continue(stat.Start.Line, stat.Start.Column, stat.Stop.Line, stat.Stop.Column);
        }

        private ExpressionStatement makeExpressionStatement(StatementContext stat)
        {
            Expression expression = (Expression)VisitExpression(stat.statementExpression);

            if (!expression.IsExpressionStatement) {
                log.error(expression.Pos, messages.expressionStatement);
            }

            return new ExpressionStatement(expression.beginLine, expression.beginCol,
                stat.Stop.Line, stat.Stop.Column, expression);
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

        private IList<StatementNode> getForInit(ForInitContext forInit)
        {
            if (forInit == null) {
                return CollectionUtils.emptyList<StatementNode>();
            }

            ExpressionListContext statementExpressionList = forInit.expressionList();
            if (statementExpressionList != null) {
                IList<ExpressionContext> statementExpressionContexts = statementExpressionList._statements;
                StatementNode[] statements = new StatementNode[statementExpressionContexts.Count];
                for (var i = 0; i < statementExpressionContexts.Count; i++) {
                    Expression expr = (Expression)VisitExpression(statementExpressionContexts[i]);
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

        private IList<Expression> getForUpdate(ExpressionListContext forUpdate)
        {
            if (forUpdate == null) {
                return CollectionUtils.emptyList<Expression>();
            }
            IList<ExpressionContext> expressionContexts = forUpdate._statements;
            Expression[] expressions = new Expression[expressionContexts.Count];
            for (var i = 0; i < expressionContexts.Count; i++) {
                expressions[i] = (Expression)VisitExpression(expressionContexts[i]);
            }
            return expressions;
        }

        public override Tree VisitForInit(ForInitContext context) => throw new InvalidOperationException();

        public override Tree VisitFuncInvocation(FuncInvocationContext context)
        {
            prepareInvocation(context, out IToken funcName, out Expression[] args, out int endLine, out int endCol);
            return new FuncInvocation(funcName.Line, funcName.Column, endLine, endCol, funcName.Text, args);
        }

        private MethodInvocation makeMethodInvocation(Expression receiver, FuncInvocationContext invocation)
        {
            prepareInvocation(invocation, out IToken funcName, out Expression[] args, out int endLine, out int endCol);
            return new MethodInvocation(funcName.Line, funcName.Column, endLine, endCol, receiver, funcName.Text, args);
        }

        private void prepareInvocation(FuncInvocationContext context, out IToken funcName, out Expression[] args,
                                       out int endLine, out int endCol)
        {
            funcName = context.neme;
            ArgumentListContext argumentList = context.argumentList();
            IList<ExpressionContext> parsedArgs = argumentList != null
                ? argumentList._args
                : CollectionUtils.emptyList<ExpressionContext>();

            args = new Expression[parsedArgs.Count];

            for (var i = 0; i < parsedArgs.Count; i++) {
                args[i] = (Expression)VisitExpression(parsedArgs[i]);
            }

            IToken stopToken = context.Stop;
            endLine = stopToken.Line;
            endCol = stopToken.StopIndex - stopToken.StartIndex + 1;
        }

        public override Tree VisitArgumentList(ArgumentListContext context) => throw new InvalidOperationException();

        public ConditionalExpression makeConditional(ExpressionContext context)
        {
            Expression condition = (Expression)VisitExpression(context.condition);
            Expression ifTrue = (Expression)VisitExpression(context.ifTrue);
            Expression ifFalse = (Expression)VisitExpression(context.ifFalse);
            return new ConditionalExpression(condition, ifTrue, ifFalse);
        }

        public override Tree VisitExpression(ExpressionContext context)
        {
            PrimaryContext primaryCtx = context.primary();
            if (primaryCtx != null) {
                Expression expr = (Expression)VisitPrimary(primaryCtx);
                FuncInvocationContext methodInvocation = context.funcInvocation();
                if (methodInvocation != null) {
                    expr = makeMethodInvocation(expr, methodInvocation);
                }
                return expr;
            }
            FuncInvocationContext invocation = context.funcInvocation();
            if (invocation != null) {
                return (Expression)VisitFuncInvocation(invocation);
            }
            if (context.NEW() != null) {
                return context.Identifier() != null
                    ? makeNewStruct(context)
                    : makeNewArray(context);
            }
            if (context.index != null) {
                return makeArrayIndex(context);
            }
            IToken postfix = context.postfix;
            if (postfix != null) {
                return makeUnary(context, postfix.Type);
            }
            IToken prefix = context.prefix;
            if (prefix != null) {
                return makeUnary(context, prefix.Type);
            }
            IToken binOp = context.bop;
            if (binOp != null) {
                if (binOp.Type == DOT) {
                    return makeSelect(context);
                }
                return context.isAssignment
                    ? makeAssignment(context)
                    : makeBinary(context);
            }
            return makeConditional(context);
        }

        private Select makeSelect(ExpressionContext context)
        {
            Expression left = (Expression)VisitExpression(context.left);

            return new Select(context.bop.Line, context.bop.Column,
                context.stop.Line, context.stop.Column, left, context.Identifier().GetText());
        }

        private ArrayIndex makeArrayIndex(ExpressionContext context)
        {
            Expression indexBase = (Expression)VisitExpression(context.indexBase);
            Expression index = (Expression)VisitExpression(context.index);

            return new ArrayIndex(context.index.start.Line, context.start.Column,
                context.index.stop.Line, context.index.stop.Column, indexBase: indexBase, index: index);
        }

        public override Tree VisitPrimary(PrimaryContext primary)
        {
            var expressionContext = primary.parenthesized;
            if (expressionContext != null) {
                return (Expression)VisitExpression(expressionContext);
            }
            LiteralContext literal = primary.literal();
            if (literal != null) {
                return (Expression)VisitLiteral(literal);
            }
            ITerminalNode identifier = primary.Identifier();
            if (identifier != null) {
                return makeIdentifier(identifier);
            }
            return new This(primary.start.Line, primary.start.Column, primary.stop.Line, primary.stop.Column);
        }

        private static Expression makeIdentifier(ITerminalNode identifierNode)
        {
            IToken symbol = identifierNode.Symbol;
            String identifier = symbol.Text;

            int stopColumn = symbol.Column + symbol.StopIndex - symbol.StopIndex;

            return new Identifier(symbol.Line, symbol.Column, symbol.Line, stopColumn, identifier);
        }

        public Tree makeNewStruct(ExpressionContext context)
        {
            return new NewStruct(context.Start.Line, context.Start.Column, context.Stop.Line,
                context.Stop.Column, context.Identifier().GetText());
        }

        public Tree makeNewArray(ExpressionContext context)
        {
            TypeTree type = (TypeTree)VisitType(context.type());
            Expression length = (Expression)VisitExpression(context.length);

            return new NewArray(context.Start.Line, context.Start.Column, context.Stop.Line,
                context.Stop.Column, type, length);
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
                        log.error(
                            new DiagnosticPosition(context.Start.Line, context.Start.Column),
                            messages.intLiteratTooBig, context.GetText()
                        );
                        type = TypeTag.ERROR;
                        value = 0;
                    }
                    break;
                case FLOAT:
                    symbol = context.FloatingPointLiteral().Symbol;
                    text = symbol.Text;
                    if (text.EndsWith("f") || text.EndsWith("F")) {
                        text = text.Substring(0, text.Length - 1);
                        type = TypeTag.FLOAT;
                        if (Single.TryParse(text, out var floatLit)) {
                            value = floatLit;
                        } else {
                            value = 0;
                            logFloatTooBig(context);
                        }
                    } else if (text.EndsWith("d") || text.EndsWith("D")) {
                        type = TypeTag.DOUBLE;
                        if (Double.TryParse(text, out var doubleLit)) {
                            value = doubleLit;
                        } else {
                            value = 0;
                            logFloatTooBig(context);
                        }
                    } else if (Single.TryParse(text, out var floatLit)) {
                        type = TypeTag.FLOAT;
                        value = floatLit;
                    } else if (Double.TryParse(text, out var doubleLit)) {
                        type = TypeTag.DOUBLE;
                        value = doubleLit;
                    } else {
                        type = TypeTag.ERROR;
                        value = 0;
                        logFloatTooBig(context);
                    }
                    break;
                case BOOLEAN:
                    symbol = context.BooleanLiteral().Symbol;
                    text = symbol.Text;
                    type = TypeTag.BOOLEAN;
                    value = Boolean.Parse(text);
                    break;
                case CHAR:
                    symbol = context.CharLiteral().Symbol;
                    text = symbol.Text;
                    type = TypeTag.CHAR;
                    value = parseCharLiteral(text);
                    break;
                case CSTRING:
                    symbol = context.CStringLiteral().Symbol;
                    text = symbol.Text;
                    type = TypeTag.C_STRING;
                    value = parseStringLiteral(text);
                    break;
                case NULL:
                    value = null;
                    symbol = context.NULL().Symbol;
                    type = TypeTag.NULL;
                    text = "null";
                    break;
                default:
                    throw new InvalidOperationException();
            }

            int line = symbol.Line;
            int startColumn = symbol.Column;
            int endColumn = startColumn + text.Length;

            return new LiteralExpression(line, startColumn, line, endColumn, type, value);
        }

        private void logFloatTooBig(LiteralContext litContext)
        {
            log.error(
                new DiagnosticPosition(litContext.Start.Line, litContext.Start.Column),
                messages.floatLiteratTooBig, litContext.GetText()
            );
        }

        private String parseStringLiteral(String text)
        {
            StringBuilder sb = new StringBuilder();
            // from 1 to <len-1 to ignore quotes
            for (var i = 1; i < text.Length - 1; i++) {
                char c = text[i];
                if (c == '\\') {
                    switch (text[++i]) {
                        case 't':
                            sb.Append('\t');
                            break;
                        case 'n':
                            sb.Append('\n');
                            break;
                        case '\\':
                            sb.Append('\\');
                            break;
                    }
                } else {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        private byte parseCharLiteral(String text)
        {
            char c = text[1];
            if (c == '\\') {
                switch (text[2]) {
                    case 't':
                        return (byte)'\t';
                    case 'n':
                        return (byte)'\n';
                    case '\\':
                        return (byte)'\\';
                    case '0':
                        return 0;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(text));
                }
            }

            return (byte)c;
        }

        private UnaryExpressionNode makeUnary(ExpressionContext context, int opCode)
        {
            Tag op;
            switch (opCode) {
                case INC:
                    op = context.prefix != null ? Tag.PRE_INC : Tag.POST_INC;
                    break;
                case DEC:
                    op = context.prefix != null ? Tag.PRE_DEC : Tag.POST_DEC;
                    break;
                case SUB:
                    op = Tag.NEG;
                    break;
                default: throw new InvalidOperationException();
            }

            Expression arg = (Expression)VisitExpression(context.arg);

            return new UnaryExpressionNode(context.Start.Line, context.Start.Column, arg.endLine,
                arg.endCol, op, arg);
        }

        private BinaryExpressionNode makeBinary(ExpressionContext context)
        {
            Expression left = (Expression)VisitExpression(context.left);
            Expression right = (Expression)VisitExpression(context.right);

            Tag op;
            switch (context.bop.Type) {
                case ADD:
                    op = Tag.PLUS;
                    break;
                case SUB:
                    op = Tag.MINUS;
                    break;
                case MUL:
                    op = Tag.MUL;
                    break;
                case DIV:
                    op = Tag.DIV;
                    break;
                case MOD:
                    op = Tag.MOD;
                    break;
                case LSHIFT:
                    op = Tag.SHL;
                    break;
                case RSHIFT:
                    op = Tag.SHR;
                    break;
                case LT:
                    op = Tag.LT;
                    break;
                case LE:
                    op = Tag.LE;
                    break;
                case GT:
                    op = Tag.GT;
                    break;
                case GE:
                    op = Tag.GE;
                    break;
                case EQUAL:
                    op = Tag.EQ;
                    break;
                case NOTEQUAL:
                    op = Tag.NEQ;
                    break;
                case AND:
                    op = Tag.AND;
                    break;
                case OR:
                    op = Tag.OR;
                    break;
                case BITAND:
                    op = Tag.BITAND;
                    break;
                case BITOR:
                    op = Tag.BITOR;
                    break;
                case CARET:
                    op = Tag.BITXOR;
                    break;
                default: throw new InvalidOperationException();
            }

            return new BinaryExpressionNode(op, left, right);
        }

        private Expression makeAssignment(ExpressionContext context)
        {
            Expression left = (Expression)VisitExpression(context.left);
            Expression right = (Expression)VisitExpression(context.right);

            bool lValueError = !left.IsLValue;

            Tag op;
            switch (context.bop.Type) {
                case ASSIGN:
                    AssignNode assign = new AssignNode(left, right);
                    if (lValueError) {
                        log.error(assign.Pos, messages.assignmentLHS);
                    }
                    return assign;
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
                case MOD_ASSIGN:
                    op = Tag.MOD_ASG;
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
                case LSHIFT_ASSIGN:
                    op = Tag.SHL_ASG;
                    break;
                case RSHIFT_ASSIGN:
                    op = Tag.SHR_ASG;
                    break;
                default: throw new InvalidOperationException();
            }

            CompoundAssignNode compAssign = new CompoundAssignNode(op, left, right);
            if (lValueError) {
                log.error(compAssign.Pos, messages.assignmentLHS);
            }
            return compAssign;
        }
    }
}
