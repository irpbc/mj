using System;
using System.Collections.Generic;

using LLVMSharp;

using mj.compiler.main;
using mj.compiler.symbol;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using Type = mj.compiler.symbol.Type;

namespace mj.compiler.tree
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

        public DiagnosticPosition Pos => new DiagnosticPosition {line = beginLine, column = beginCol};
        public DiagnosticPosition EndPos => new DiagnosticPosition {line = endLine, column = endCol};

        public abstract Tag Tag { get; }

        public abstract void accept(AstVisitor v);
        public abstract T accept<T>(AstVisitor<T> v);
        public abstract T accept<T, A>(AstVisitor<T, A> v, A arg);
    }

    public sealed class CompilationUnit : Tree
    {
        public SourceFile sourceFile;
        public IList<Tree> declarations;
        public Scope.WriteableScope topLevelScope;

        public CompilationUnit(int beginLine, int beginCol, int endLine, int endCol,
                               IList<Tree> declarations)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.declarations = declarations;
        }

        public override Tag Tag => Tag.COMPILATION_UNIT;

        public override void accept(AstVisitor v) => v.visitCompilationUnit(this);
        public override T accept<T>(AstVisitor<T> v) => v.visitCompilationUnit(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitCompilationUnit(this, arg);
    }

    public sealed class ClassDef : Tree
    {
        public String name;
        public IList<VariableDeclaration> fields;
        public Symbol.ClassSymbol symbol;

        public ClassDef(int beginLine, int beginCol, int endLine, int endCol, string name, VariableDeclaration[] fields) 
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.name = name;
            this.fields = fields;
        }

        public override Tag Tag => Tag.CLASS_DEF;
        
        public override void accept(AstVisitor v) => v.visitClassDef(this);
        public override T accept<T>(AstVisitor<T> v) => v.visitClassDef(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitClassDef(this, arg);
    }

    public abstract class Expression : Tree
    {
        public Type type;

        protected Expression(int beginLine, int beginCol, int endLine, int endCol)
            : base(beginLine, beginCol, endLine, endCol) { }

        public virtual bool IsLValue => false;
        public virtual bool IsExpressionStatement => false;
    }

    public abstract class OperatorExpression : Expression
    {
        public Tag opcode;

        public Symbol.OperatorSymbol operatorSym;

        protected OperatorExpression(int beginLine, int beginCol, int endLine, int endCol, Tag opcode)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.opcode = opcode;
        }

        public override Tag Tag => opcode;
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

        public override void accept(AstVisitor v) => v.visitBinary(this);
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

        public override bool IsExpressionStatement => opcode.isIncDec();

        public override void accept(AstVisitor v) => v.visitUnary(this);
        public override T accept<T>(AstVisitor<T> v) => v.visitUnary(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitUnary(this, arg);
    }

    public sealed class Select : Expression
    {
        public Expression selectBase;
        public String name;
        public Symbol.VarSymbol symbol;

        public Select(int beginLine, int beginCol, int endLine, int endCol, Expression selectBase, string name) 
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.selectBase = selectBase;
            this.name = name;
        }

        public override Tag Tag => Tag.SELECT;

        public override bool IsLValue => true;

        public override void accept(AstVisitor v) => v.visitSelect(this);
        public override T accept<T>(AstVisitor<T> v) => v.visitSelect(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitSelect(this, arg);
    }

    public sealed class ArrayIndex : Expression
    {
        public Expression indexBase;
        public Expression index;

        public ArrayIndex(int beginLine, int beginCol, int endLine, int endCol, Expression indexBase, Expression index) 
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.indexBase = indexBase;
            this.index = index;
        }

        public override Tag Tag => Tag.INDEX;

        public override bool IsLValue => true;

        public override void accept(AstVisitor v) => v.visitIndex(this);
        public override T accept<T>(AstVisitor<T> v) => v.visitIndex(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitIndex(this, arg);
    }

    public sealed class NewClass : Expression
    {
        public String className;
        public Symbol.ClassSymbol symbol;

        public NewClass(int beginLine, int beginCol, int endLine, int endCol, String className) 
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.className = className;
        }

        public override Tag Tag => Tag.NEW_CLASS;
        
        public override void accept(AstVisitor v) => v.visitNewClass(this);
        public override T accept<T>(AstVisitor<T> v) => v.visitNewClass(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitNewClass(this, arg);
    }

    public sealed class NewArray : Expression
    {
        public TypeTree elemenTypeTree;
        public Expression length;

        public NewArray(int beginLine, int beginCol, int endLine, int endCol, TypeTree elemenTypeTree, Expression length) 
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.elemenTypeTree = elemenTypeTree;
            this.length = length;
        }

        public override Tag Tag => Tag.NEW_ARRAY;

        public override void accept(AstVisitor v) => v.visitNewArray(this);
        public override T accept<T>(AstVisitor<T> v) => v.visitNewArray(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitNewArray(this, arg);
    }

    public sealed class AssignNode : Expression
    {
        public Expression left;
        public Expression right;

        public AssignNode(Expression left, Expression right)
            : base(left.beginLine, left.beginCol, right.endLine, right.endCol)
        {
            this.left = left;
            this.right = right;
        }

        public override Tag Tag => Tag.ASSIGN;
        public override bool IsExpressionStatement => true;

        public override void accept(AstVisitor v) => v.visitAssign(this);
        public override T accept<T>(AstVisitor<T> v) => v.visitAssign(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitAssign(this, arg);
    }

    public sealed class CompoundAssignNode : OperatorExpression
    {
        public Expression left;
        public Expression right;

        public CompoundAssignNode(Tag opcode, Expression left, Expression right)
            : base(left.beginLine, left.beginCol, right.endLine, right.endCol, opcode)
        {
            this.left = left;
            this.right = right;
        }
        
        public override bool IsExpressionStatement => true;

        public override void accept(AstVisitor v) => v.visitCompoundAssign(this);
        public override T accept<T>(AstVisitor<T> v) => v.visitCompoundAssign(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitCompoundAssign(this, arg);
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

        public override Tag Tag => Tag.COND_EXPR;

        public override void accept(AstVisitor v) => v.visitConditional(this);
        public override T accept<T>(AstVisitor<T> v) => v.visitConditional(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitConditional(this, arg);
    }

    public sealed class LiteralExpression : Expression
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public TypeTag typeTag;

        public Object value;

        public LiteralExpression(int beginLine, int beginCol, int endLine, int endCol, TypeTag typeTag,
                                 object value)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.typeTag = typeTag;
            this.value = value;
        }

        public override Tag Tag => Tag.LITERAL;

        public override void accept(AstVisitor v) => v.visitLiteral(this);
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

        public override Tag Tag => Tag.VAR_DEF;

        public override void accept(AstVisitor v) => v.visitVarDef(this);
        public override T accept<T>(AstVisitor<T> v) => v.visitVarDef(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitVarDef(this, arg);
    }

    public sealed class MethodDef : Tree
    {
        public String name;
        public TypeTree returnType;
        public IList<VariableDeclaration> parameters;
        public IList<Annotation> annotations;
        public bool isPrivate;
        public Block body;
        public Symbol.MethodSymbol symbol;
        public bool exitsNormally;

        public MethodDef(int beginLine, int beginCol, int endLine, int endCol, string name, TypeTree returnType,
                         IList<VariableDeclaration> parameters, IList<Annotation> annotations, Block body,
                         bool isPrivate)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.name = name;
            this.returnType = returnType;
            this.parameters = parameters;
            this.annotations = annotations;
            this.body = body;
            this.isPrivate = isPrivate;
        }

        public override Tag Tag => Tag.METHOD_DEF;

        public override void accept(AstVisitor v) => v.visitMethodDef(this);
        public override T accept<T>(AstVisitor<T> v) => v.visitMethodDef(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitMethodDef(this, arg);
    }

    public class AspectDef : Tree
    {
        public String name;
        public MethodDef after;
        public Symbol.AspectSymbol symbol;

        public AspectDef(int beginLine, int beginCol, int endLine, int endCol, String name, MethodDef after)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.name = name;
            this.after = after;
        }

        public override Tag Tag => Tag.ASPECT_DEF;

        public override void accept(AstVisitor v) => v.visitAspectDef(this);
        public override T accept<T>(AstVisitor<T> v) => v.visitAspectDef(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitAspectDef(this, arg);
    }

    public class Annotation : Tree
    {
        public String name;
        public Symbol.AspectSymbol symbol;

        public Annotation(string name, int beginLine, int beginCol, int endLine, int endCol)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.name = name;
        }

        public override Tag Tag => Tag.ANNOTATION;

        public override void accept(AstVisitor v) => v.visitAnnotation(this);
        public override T accept<T>(AstVisitor<T> v) => v.visitAnnotation(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitAnnotation(this, arg);
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

        public override Tag Tag => Tag.PRIM_TYPE;

        public override void accept(AstVisitor v) => v.visitPrimitiveType(this);
        public override T accept<T>(AstVisitor<T> v) => v.visitPrimitiveType(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitPrimitiveType(this, arg);
    }

    public sealed class DeclaredType : TypeTree
    {
        public String name;

        public DeclaredType(int beginLine, int beginCol, int endLine, int endCol, String name) 
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.name = name;
        }

        public override Tag Tag => Tag.DECLARED_TYPE;

        public override void accept(AstVisitor v) => v.visitDeclaredType(this);
        public override T accept<T>(AstVisitor<T> v) => v.visitDeclaredType(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitDeclaredType(this, arg);
    }

    public sealed class ArrayTypeTree : TypeTree
    {
        public TypeTree elemTypeTree;

        public ArrayTypeTree(int beginLine, int beginCol, int endLine, int endCol, TypeTree elemTypeTree) 
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.elemTypeTree = elemTypeTree;
        }

        public override Tag Tag => Tag.ARRAY_TYPE;

        public override void accept(AstVisitor v) => v.visitArrayType(this);
        public override T accept<T>(AstVisitor<T> v) => v.visitArrayType(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitArrayType(this, arg);
    }

    public sealed class Identifier : Expression
    {
        public String name;
        public Symbol.VarSymbol symbol;

        public Identifier(int beginLine, int beginCol, int endLine, int endCol, string name)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.name = name;
        }

        public override Tag Tag => Tag.IDENT;
        public override bool IsLValue => true;

        public override void accept(AstVisitor v) => v.visitIdent(this);
        public override T accept<T>(AstVisitor<T> v) => v.visitIdent(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitIdent(this, arg);
    }

    public sealed class MethodInvocation : Expression
    {
        public String methodName;
        public IList<Expression> args;
        public Symbol.MethodSymbol methodSym;

        public MethodInvocation(int beginLine, int beginCol, int endLine, int endCol, string methodName,
                                IList<Expression> args) : base(beginLine, beginCol, endLine, endCol)
        {
            this.methodName = methodName;
            this.args = args;
        }

        public override Tag Tag => Tag.INVOKE;
        public override bool IsExpressionStatement => true;

        public override void accept(AstVisitor v) => v.visitMethodInvoke(this);
        public override T accept<T>(AstVisitor<T> v) => v.visitMethodInvoke(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitMethodInvoke(this, arg);
    }

    public abstract class StatementNode : Tree
    {
        protected StatementNode(int beginLine, int beginCol, int endLine, int endCol)
            : base(beginLine, beginCol, endLine, endCol) { }

        public virtual LLVMBasicBlockRef BreakBlock {
            get => default(LLVMBasicBlockRef);
            set { }
        }

        public virtual LLVMBasicBlockRef ContinueBlock {
            get => default(LLVMBasicBlockRef);
            set { }
        }

        public virtual void setBreak() { }
    }

    public sealed class ReturnStatement : StatementNode
    {
        public Expression value;
        public IList<Expression> afterAspects;

        public ReturnStatement(int beginLine, int beginCol, int endLine, int endCol, Expression value)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.value = value;
        }

        public override Tag Tag => Tag.RETURN;

        public override void accept(AstVisitor v) => v.visitReturn(this);
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

        public override Tag Tag => Tag.BLOCK;

        public override void accept(AstVisitor v) => v.visitBlock(this);
        public override T accept<T>(AstVisitor<T> v) => v.visitBlock(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitBlock(this, arg);
    }

    public abstract class JumpStatement : StatementNode
    {
        public StatementNode target;
        public LLVMValueRef instruction;

        protected JumpStatement(int beginLine, int beginCol, int endLine, int endCol)
            : base(beginLine, beginCol, endLine, endCol) { }
    }

    public sealed class Break : JumpStatement
    {
        public Break(int beginLine, int beginCol, int endLine, int endCol)
            : base(beginLine, beginCol, endLine, endCol) { }

        public override Tag Tag => Tag.BREAK;

        public override void accept(AstVisitor v) => v.visitBreak(this);
        public override T accept<T>(AstVisitor<T> v) => v.visitBreak(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitBreak(this, arg);
    }

    public sealed class Continue : JumpStatement
    {
        public Continue(int beginLine, int beginCol, int endLine, int endCol)
            : base(beginLine, beginCol, endLine, endCol) { }

        public override Tag Tag => Tag.CONTINUE;

        public override void accept(AstVisitor v) => v.visitContinue(this);
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

        public override Tag Tag => Tag.IF;

        public override void accept(AstVisitor v) => v.visitIf(this);
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

        public override Tag Tag => Tag.WHILE;

        public override LLVMBasicBlockRef BreakBlock { get; set; }
        public override LLVMBasicBlockRef ContinueBlock { get; set; }

        public override void accept(AstVisitor v) => v.visitWhile(this);
        public override T accept<T>(AstVisitor<T> v) => v.visitWhile(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitWhileLoop(this, arg);
    }

    public sealed class DoStatement : WhileStatement
    {
        public DoStatement(int beginLine, int beginCol, int endLine, int endCol, Expression condition,
                           StatementNode body)
            : base(beginLine, beginCol, endLine, endCol, condition, body) { }

        public override Tag Tag => Tag.DO;

        public override LLVMBasicBlockRef BreakBlock { get; set; }
        public override LLVMBasicBlockRef ContinueBlock { get; set; }

        public override void accept(AstVisitor v) => v.visitDo(this);
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

        public override Tag Tag => Tag.FOR;

        public override LLVMBasicBlockRef BreakBlock { get; set; }
        public override LLVMBasicBlockRef ContinueBlock { get; set; }

        public override void accept(AstVisitor v) => v.visitFor(this);
        public override T accept<T>(AstVisitor<T> v) => v.visitFor(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitForLoop(this, arg);
    }

    public sealed class Switch : StatementNode
    {
        public Expression selector;
        public IList<Case> cases;
        public bool hasDefault;
        public bool didBreak;

        public Switch(int beginLine, int beginCol, int endLine, int endCol, Expression selector, IList<Case> cases,
                      bool hasDefault)
            : base(beginLine, beginCol, endLine, endCol)
        {
            this.selector = selector;
            this.cases = cases;
            this.hasDefault = hasDefault;
        }

        public override LLVMBasicBlockRef BreakBlock { get; set; }

        public override void setBreak()
        {
            this.didBreak = true;
        }

        public override Tag Tag => Tag.SWITCH;

        public override void accept(AstVisitor v) => v.visitSwitch(this);
        public override T accept<T>(AstVisitor<T> v) => v.visitSwitch(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitSwitch(this, arg);
    }

    public sealed class Case : Tree
    {
        public Expression expression;
        public IList<StatementNode> statements;

        public Case(int beginLine, int beginCol, int endLine, int endCol, Expression expression,
                    IList<StatementNode> statements) : base(beginLine, beginCol, endLine, endCol)
        {
            this.expression = expression;
            this.statements = statements;
        }

        public override Tag Tag => Tag.CASE;

        public override void accept(AstVisitor v) => v.visitCase(this);
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

        public override Tag Tag => Tag.EXEC;

        public override void accept(AstVisitor v) => v.visitExpresionStmt(this);
        public override T accept<T>(AstVisitor<T> v) => v.visitExpresionStmt(this);
        public override T accept<T, A>(AstVisitor<T, A> v, A arg) => v.visitExpresionStmt(this, arg);
    }

    /// <summary>
    /// Order of enum constants is important for extension methods below.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum Tag
    {
        // Loop statements
        FOR,
        WHILE,
        DO,

        // Other statements
        IF,
        BLOCK,
        INVOKE,
        METHOD_DEF,
        ASPECT_DEF,
        ANNOTATION,
        VAR_DEF,
        COMPILATION_UNIT,
        CLASS_DEF,
        EXEC,
        SWITCH,
        CASE,
        BREAK,
        CONTINUE,
        COND_EXPR,
        LITERAL,
        PRIM_TYPE,
        DECLARED_TYPE,
        ARRAY_TYPE,
        IDENT,
        SELECT,
        INDEX,
        NEW_CLASS,
        NEW_ARRAY,
        RETURN,

        // Assign expression
        ASSIGN,

        // Unary operators
        NEG, // -
        NOT, // !
        COMPL, // ~
        PRE_INC, // ++ _
        PRE_DEC, // -- _
        POST_INC, // _ ++
        POST_DEC, // _ --

        // Logical binary operators
        OR, // ||
        AND, // &&
        EQ, // ==
        NEQ, // !=
        LT, // <
        GT, // >
        LE, // <=
        GE, // >=

        // Numeric binary operators
        BITOR, // |
        BITXOR, // ^
        BITAND, // &
        SHL, // <<
        SHR, // >>
        PLUS, // +
        MINUS, // -
        MUL, // *
        DIV, // /
        MOD, // %

        // Compound assign numeric operators
        BITOR_ASG, // |=
        BITXOR_ASG, // ^=
        BITAND_ASG, // &=
        SHL_ASG, // <<=
        SHR_ASG, // >>=
        PLUS_ASG, // +=
        MINUS_ASG, // -=
        MUL_ASG, // *=
        DIV_ASG, // /=
        MOD_ASG, // %=
    }

    public static class TagExtensions
    {
        // Relies on order of enum constants for loops!
        public static bool isLoop(this Tag tag)
        {
            return tag >= Tag.FOR && tag <= Tag.DO;
        }

        // Relies on order of enum constants!
        public static bool isIncDec(this Tag tag)
        {
            return tag >= Tag.PRE_INC && tag <= Tag.POST_DEC;
        }

        public static bool isPre(this Tag tag)
        {
            return tag == Tag.PRE_INC || tag == Tag.PRE_DEC;
        }

        // Relies on NEG being the first operator declared in enum!
        public static int operatorIndex(this Tag opTag)
        {
            return opTag - Tag.NEG;
        }

        // Find base operator for a given compound assignment operator.
        // Relies on order of enum constants!
        public static Tag baseOperator(this Tag compAssTag)
        {
            return compAssTag - (Tag.MOD - Tag.BITOR + 1);
        }

        public static bool isComparison(this Tag tag)
        {
            return tag >= Tag.EQ && tag <= Tag.GE;
        }
    }
}
