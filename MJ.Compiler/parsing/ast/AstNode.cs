using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace mj.compiler.parsing.ast
{
    public abstract class AstNode
    {
        public readonly int beginLine;
        public readonly int beginCol;
        public int endLine;
        public int endCol;

        protected AstNode(int beginLine, int beginCol, int endLine, int endCol)
        {
            this.beginLine = beginLine;
            this.beginCol = beginCol;
            this.endLine = endLine;
            this.endCol = endCol;
        }

        public abstract T accept<T>(AstVisitor<T> v);
    }

    public sealed class CompilatioUnit : AstNode
    {
        public IList<MethodNode> methods;

        public CompilatioUnit(IList<MethodNode> methods)
            : base(methods[0].beginLine, methods[0].beginCol,
                methods[methods.Count - 1].endLine, methods[methods.Count - 1].endCol)
        {
            this.methods = methods;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitCompilationUnit(this);
    }

    public abstract class ExpressionNode : AstNode
    {
        protected ExpressionNode(int beginLine, int beginCol, int endLine, int endCol)
            : base(beginLine, beginCol, endLine, endCol) { }
    }

    public sealed class BinaryExpressionNode : ExpressionNode
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public Operator op;
        public ExpressionNode left;
        public ExpressionNode right;

        public BinaryExpressionNode(Operator op, ExpressionNode left, ExpressionNode right)
            : base(left.beginLine, left.beginCol, right.endLine, right.endCol)
        {
            this.op = op;
            this.left = left;
            this.right = right;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitBinary(this);
    }

    public sealed class UnaryExpressionNode : ExpressionNode
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public Operator op;
        public ExpressionNode operand;

        public UnaryExpressionNode(int beginLine, int beginCol, int endLine, int endCol, Operator op,
                                   ExpressionNode operand)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.op = op;
            this.operand = operand;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitUnary(this);
    }

    public sealed class ConditionalExpressionNode : ExpressionNode
    {
        public ExpressionNode condition;
        public ExpressionNode ifTrue;
        public ExpressionNode ifFalse;

        public ConditionalExpressionNode(ExpressionNode condition, ExpressionNode ifTrue, ExpressionNode ifFalse)
            : base(condition.beginLine, condition.beginCol, ifFalse.endLine, ifFalse.endCol)
        {
            this.condition = condition;
            this.ifTrue = ifTrue;
            this.ifFalse = ifFalse;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitConditional(this);
    }

    public sealed class LiteralExpressionNode : ExpressionNode
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public LiteralType type;
        public Object value;

        public LiteralExpressionNode(int beginLine, int beginCol, int endLine, int endCol, LiteralType literalType,
                                     object value)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.type = literalType;
            this.value = value;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitLiteral(this);
    }

    public abstract class VariableDeclaration : StatementNode
    {
        public String name;
        public TypeRefNode type;

        protected VariableDeclaration(int beginLine, int beginCol, int endLine, int endCol, string name,
                                      TypeRefNode type)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.name = name;
            this.type = type;
        }
    }

    public sealed class MethodNode : AstNode
    {
        public String name;
        public TypeRefNode returnType;
        public IList<MethodParameter> parameters;
        public Block body;

        public MethodNode(int beginLine, int beginCol, int endLine, int endCol, string name,
                          TypeRefNode returnType, IList<MethodParameter> parameters, Block body)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.name = name;
            this.returnType = returnType;
            this.parameters = parameters;
            this.body = body;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitMethodDef(this);
    }

    public sealed class MethodParameter : VariableDeclaration
    {
        public MethodParameter(int beginLine, int beginCol, int endLine, int endCol, string name, TypeRefNode type)
            : base(beginLine, beginCol, endLine, endCol, name, type) { }

        public override T accept<T>(AstVisitor<T> v) => v.visitParam(this);
    }

    public abstract class TypeRefNode : AstNode
    {
        protected TypeRefNode(int beginLine, int beginCol, int endLine, int endCol)
            : base(beginLine, beginCol, endLine, endCol) { }
    }

    public sealed class PrimitiveTypeNode : TypeRefNode
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public PrimitiveType type;

        public PrimitiveTypeNode(int beginLine, int beginCol, int endLine, int endCol, PrimitiveType type)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.type = type;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitPrimitiveType(this);
    }

    public sealed class LocalVariableDeclaration : VariableDeclaration
    {
        public ExpressionNode init;

        public LocalVariableDeclaration(int beginLine, int beginCol, int endLine, int endCol, string name,
                                        TypeRefNode type, ExpressionNode init)
            : base(beginLine, beginCol, endLine, endCol, name, type)
        {
            this.init = init;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitLocalVar(this);
    }

    public sealed class IdentifierNode : ExpressionNode
    {
        public String name;

        public IdentifierNode(int beginLine, int beginCol, int endLine, int endCol, string name)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.name = name;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitIdent(this);
    }

    public sealed class MethodInvocation : ExpressionNode
    {
        public String methodName;
        public IList<ExpressionNode> args;

        public MethodInvocation(int beginLine, int beginCol, int endLine, int endCol, string methodName,
                                IList<ExpressionNode> args) : base(beginLine, beginCol, endLine, endCol)
        {
            this.methodName = methodName;
            this.args = args;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitMethodInvoke(this);
    }

    public abstract class StatementNode : AstNode
    {
        protected StatementNode(int beginLine, int beginCol, int endLine, int endCol)
            : base(beginLine, beginCol, endLine, endCol) { }
    }

    public sealed class ReturnStatement : StatementNode
    {
        public ExpressionNode value;

        public ReturnStatement(int beginLine, int beginCol, int endLine, int endCol, ExpressionNode value)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.value = value;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitReturn(this);
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
    }

    public sealed class Break : StatementNode
    {
        public Break(int beginLine, int beginCol, int endLine, int endCol)
            : base(beginLine, beginCol, endLine, endCol) { }

        public override T accept<T>(AstVisitor<T> v) => v.visitBreak(this);
    }

    public sealed class Continue : StatementNode
    {
        public Continue(int beginLine, int beginCol, int endLine, int endCol)
            : base(beginLine, beginCol, endLine, endCol) { }

        public override T accept<T>(AstVisitor<T> v) => v.visitContinue(this);
    }

    public sealed class If : StatementNode
    {
        public ExpressionNode condition;
        public StatementNode thenPart;
        public StatementNode elsePart;

        public If(int beginLine, int beginCol, int endLine, int endCol,
                  ExpressionNode condition, StatementNode thenPart, StatementNode elsePart)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.condition = condition;
            this.thenPart = thenPart;
            this.elsePart = elsePart;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitIf(this);
    }

    public class WhileStatement : StatementNode
    {
        public ExpressionNode condition;
        public StatementNode body;

        public WhileStatement(int beginLine, int beginCol, int endLine, int endCol,
                              ExpressionNode condition, StatementNode body)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.condition = condition;
            this.body = body;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitWhile(this);
    }

    public sealed class DoStatement : WhileStatement
    {
        public DoStatement(int beginLine, int beginCol, int endLine, int endCol, ExpressionNode condition,
                           StatementNode body)
            : base(beginLine, beginCol, endLine, endCol, condition, body) { }

        public override T accept<T>(AstVisitor<T> v) => v.visitDo(this);
    }

    public sealed class ForLoop : StatementNode
    {
        public IList<StatementNode> init;
        public ExpressionNode condition;
        public IList<ExpressionNode> update;
        public StatementNode body;

        public ForLoop(int beginLine, int beginCol, IList<StatementNode> init,
                       ExpressionNode condition, IList<ExpressionNode> update, StatementNode body)
            : base(beginLine, beginCol, body.endLine, body.endCol)
        {
            this.init = init;
            this.condition = condition;
            this.update = update;
            this.body = body;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitFor(this);
    }

    public sealed class Switch : StatementNode
    {
        public ExpressionNode selector;
        public IList<Case> cases;

        public Switch(int beginLine, int beginCol, int endLine, int endCol, ExpressionNode selector, IList<Case> cases)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.selector = selector;
            this.cases = cases;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitSwitch(this);
    }

    public sealed class Case : AstNode
    {
        public ExpressionNode expression;
        public IList<StatementNode> Statements;

        public Case(int beginLine, int beginCol, int endLine, int endCol, ExpressionNode expression,
                    IList<StatementNode> statements) : base(beginLine, beginCol, endLine, endCol)
        {
            this.expression = expression;
            Statements = statements;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitCase(this);
    }

    public sealed class ExpressionStatement : StatementNode
    {
        public ExpressionNode expression;

        public ExpressionStatement(int beginLine, int beginCol, int endLine, int endCol, ExpressionNode expression)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.expression = expression;
        }

        public override T accept<T>(AstVisitor<T> v) => v.visitExpresionStmt(this);
    }

    public enum Operator
    {
        ADD,
        SUB,
        MUL,
        DIV,
        MOD,
        GT,
        LT,
        BANG,
        TILDE,
        QUESTION,
        COLON,
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
        CARET,
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

    public enum PrimitiveType
    {
        INT,
        LONG,
        FLOAT,
        DOUBLE,
        BOOLEAN,
        VOID
    }

    public enum LiteralType
    {
        INT,
        LONG,
        FLOAT,
        DOUBLE,
        BOOLEAN
    }
}
