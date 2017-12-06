using System;
using System.Collections.Generic;

using mj.compiler.main;
using mj.compiler.symbol;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using Type = mj.compiler.symbol.Type;

namespace mj.compiler.parsing.ast
{
    public abstract class Tree
    {
        [JsonIgnore]
        public readonly int beginLine;

        [JsonIgnore]
        public readonly int beginCol;

        [JsonIgnore]
        public int endLine;

        [JsonIgnore]
        public int endCol;

        protected Tree(int beginLine, int beginCol, int endLine, int endCol)
        {
            this.beginLine = beginLine;
            this.beginCol = beginCol;
            this.endLine = endLine;
            this.endCol = endCol;
        }

        public abstract T accept<T>(AstVisitor<T> v);
        public abstract T accept<T, A>(AstVisitor<T, A> v, A arg);
    }

    public sealed class CompilationUnit : Tree
    {
        public SourceFile sourceFile;
        public IList<MethodDef> methods;
        public Scope.WriteableScope topLevelScope;

        public CompilationUnit(int beginLine, int beginCol, int endLine, int endCol, IList<MethodDef> methods)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.methods = methods;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitCompilationUnit(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitCompilationUnit(this, arg);
    }

    public abstract class Expression : Tree
    {
        public Type type;

        protected Expression(int beginLine, int beginCol, int endLine, int endCol)
            : base(beginLine, beginCol, endLine, endCol) { }
    }

    public abstract class OperatorExpression : Expression
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public Tag opcode;
        public Symbol.OperatorSymbol symbol;

        protected OperatorExpression(int beginLine, int beginCol, int endLine, int endCol, Tag opcode)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.opcode = opcode;
        }
    }

    public sealed class BinaryExpressionNode : OperatorExpression
    {
        public Expression left;
        public Expression right;

        public BinaryExpressionNode(Tag opcode, Expression left, Expression right)
            : base(left.beginLine, left.beginCol, right.endLine, right.endCol, opcode)
        {
            this.left = left;
            this.right = right;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitBinary(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitBinary(this, arg);
    }

    public sealed class UnaryExpressionNode : OperatorExpression
    {
        public Expression operand;

        public UnaryExpressionNode(int beginLine, int beginCol, int endLine, int endCol,
                                   Tag opcode, Expression operand)
            : base(beginLine, beginCol, endLine, endCol, opcode)
        {
            this.operand = operand;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitUnary(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitUnary(this, arg);
    }

    public sealed class ConditionalExpression : Expression
    {
        public Expression condition;
        public Expression ifTrue;
        public Expression ifFalse;

        public ConditionalExpression(Expression condition, Expression ifTrue, Expression ifFalse)
            : base(condition.beginLine, condition.beginCol, ifFalse.endLine, ifFalse.endCol)
        {
            this.condition = condition;
            this.ifTrue = ifTrue;
            this.ifFalse = ifFalse;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitConditional(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitConditional(this, arg);
    }

    public sealed class LiteralExpression : Expression
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public TypeTag type;

        public Object value;

        public LiteralExpression(int beginLine, int beginCol, int endLine, int endCol, TypeTag typeTag,
                                 object value)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.type = typeTag;
            this.value = value;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitLiteral(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitLiteral(this, arg);
    }

    public class VariableDeclaration : StatementNode
    {
        public String name;
        public TypeTree type;
        public Symbol.VarSymbol symbol;
        public Expression init;

        public VariableDeclaration(int beginLine, int beginCol, int endLine, int endCol, string name,
                                   TypeTree type, Expression init)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.name = name;
            this.type = type;
            this.init = init;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitVarDef(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitVarDef(this, arg);
    }

    public sealed class MethodDef : Tree
    {
        public String name;
        public TypeTree returnType;
        public IList<VariableDeclaration> parameters;
        public Block body;
        public Symbol.MethodSymbol symbol;

        public MethodDef(int beginLine, int beginCol, int endLine, int endCol, string name,
                         TypeTree returnType, IList<VariableDeclaration> parameters, Block body)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.name = name;
            this.returnType = returnType;
            this.parameters = parameters;
            this.body = body;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitMethodDef(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitMethodDef(this, arg);
    }

    public abstract class TypeTree : Tree
    {
        protected TypeTree(int beginLine, int beginCol, int endLine, int endCol)
            : base(beginLine, beginCol, endLine, endCol) { }
    }

    public sealed class PrimitiveTypeNode : TypeTree
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public TypeTag type;

        public PrimitiveTypeNode(int beginLine, int beginCol, int endLine, int endCol, TypeTag type)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.type = type;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitPrimitiveType(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitPrimitiveType(this, arg);
    }

    public sealed class Identifier : Expression
    {
        public String name;

        public Identifier(int beginLine, int beginCol, int endLine, int endCol, string name)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.name = name;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitIdent(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitIdent(this, arg);
    }

    public sealed class MethodInvocation : Expression
    {
        public String methodName;
        public IList<Expression> args;

        public MethodInvocation(int beginLine, int beginCol, int endLine, int endCol, string methodName,
                                IList<Expression> args) : base(beginLine, beginCol, endLine, endCol)
        {
            this.methodName = methodName;
            this.args = args;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitMethodInvoke(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitMethodInvoke(this, arg);
    }

    public abstract class StatementNode : Tree
    {
        protected StatementNode(int beginLine, int beginCol, int endLine, int endCol)
            : base(beginLine, beginCol, endLine, endCol) { }
    }

    public sealed class ReturnStatement : StatementNode
    {
        public Expression value;

        public ReturnStatement(int beginLine, int beginCol, int endLine, int endCol, Expression value)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.value = value;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitReturn(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitReturn(this, arg);
    }

    public sealed class Block : StatementNode
    {
        public IList<StatementNode> statements;

        public Block(int beginLine, int beginCol, int endLine, int endCol, IList<StatementNode> statements)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.statements = statements;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitBlock(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitBlock(this, arg);
    }

    public sealed class Break : StatementNode
    {
        public Break(int beginLine, int beginCol, int endLine, int endCol)
            : base(beginLine, beginCol, endLine, endCol) { }

        public override T accept<T>(AstVisitor<T> v) => v.visitBreak(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitBreak(this, arg);
    }

    public sealed class Continue : StatementNode
    {
        public Continue(int beginLine, int beginCol, int endLine, int endCol)
            : base(beginLine, beginCol, endLine, endCol) { }

        public override T accept<T>(AstVisitor<T> v) => v.visitContinue(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitContinue(this, arg);
    }

    public sealed class If : StatementNode
    {
        public Expression condition;
        public StatementNode thenPart;
        public StatementNode elsePart;

        public If(int beginLine, int beginCol, int endLine, int endCol,
                  Expression condition, StatementNode thenPart, StatementNode elsePart)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.condition = condition;
            this.thenPart = thenPart;
            this.elsePart = elsePart;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitIf(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitIf(this, arg);
    }

    public class WhileStatement : StatementNode
    {
        public Expression condition;
        public StatementNode body;

        public WhileStatement(int beginLine, int beginCol, int endLine, int endCol,
                              Expression condition, StatementNode body)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.condition = condition;
            this.body = body;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitWhile(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitWhile(this, arg);
    }

    public sealed class DoStatement : WhileStatement
    {
        public DoStatement(int beginLine, int beginCol, int endLine, int endCol, Expression condition,
                           StatementNode body)
            : base(beginLine, beginCol, endLine, endCol, condition, body) { }

        public override T accept<T>(AstVisitor<T> v) => v.visitDo(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitDo(this, arg);
    }

    public sealed class ForLoop : StatementNode
    {
        public IList<StatementNode> init;
        public Expression condition;
        public IList<Expression> update;
        public StatementNode body;

        public ForLoop(int beginLine, int beginCol, IList<StatementNode> init,
                       Expression condition, IList<Expression> update, StatementNode body)
            : base(beginLine, beginCol, body.endLine, body.endCol)
        {
            this.init = init;
            this.condition = condition;
            this.update = update;
            this.body = body;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitFor(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitFor(this, arg);
    }

    public sealed class Switch : StatementNode
    {
        public Expression selector;
        public IList<Case> cases;

        public Switch(int beginLine, int beginCol, int endLine, int endCol, Expression selector, IList<Case> cases)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.selector = selector;
            this.cases = cases;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitSwitch(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitSwitch(this, arg);
    }

    public sealed class Case : Tree
    {
        public Expression expression;
        public IList<StatementNode> Statements;

        public Case(int beginLine, int beginCol, int endLine, int endCol, Expression expression,
                    IList<StatementNode> statements) : base(beginLine, beginCol, endLine, endCol)
        {
            this.expression = expression;
            Statements = statements;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitCase(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitCase(this, arg);
    }

    public sealed class ExpressionStatement : StatementNode
    {
        public Expression expression;

        public ExpressionStatement(int beginLine, int beginCol, int endLine, int endCol, Expression expression)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.expression = expression;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitExpresionStmt(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitExpresionStmt(this, arg);
    }

    public enum Tag
    {
        PLUS,
        MINUS,
        MUL,
        DIV,
        MOD,
        GT,
        LT,
        BANG,
        TILDE,
        EQ,
        LE,
        GE,
        NEQ,
        AND,
        OR,
        XOR,
        INC,
        DEC,
        POST_INC,
        POST_DEC,
        BITAND,
        BITOR,
        LSHIFT,
        RSHIFT,
        ASSIGN,
        ADD_ASSIGN,
        SUB_ASSIGN,
        MUL_ASSIGN,
        DIV_ASSIGN,
        AND_ASSIGN,
        OR_ASSIGN,
        XOR_ASSIGN,
        MOD_ASSIGN,
        LSHIFT_ASSIGN,
        RSHIFT_ASSIGN
    }
}
