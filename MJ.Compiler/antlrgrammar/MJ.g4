grammar MJ;

@header {
using System;
}

@members {
    //public enum LiteralType { INTEGER, FLOAT, BOOLEAN }
}

literal returns [ int literalType ]
    : IntegerLiteral       { $literal::literalType=INT; }
    | FloatingPointLiteral { $literal::literalType=FLOAT; }
    | BooleanLiteral       { $literal::literalType=BOOLEAN; }
    | CStringLiteral       { $literal::literalType=CSTRING; }
    | CharLiteral          { $literal::literalType=CHAR; }
    | NULL                 { $literal::literalType=NULL; }
    ;

type returns [ int arrays ]
    : (primitive='int'
    | primitive='long'
    | primitive='float'
    | primitive='double'
    | primitive='boolean'
    | primitive='char'
    | primitive='cstring'
    | structName=Identifier) ('[' ']' { $type::arrays++; } )*
;

compilationUnit : declarations+=declaration+ EOF ;

declaration
    : structDef
    | funcDeclaration
    ;
    
structDef
    : 'struct' name=Identifier '{' (members+=memberDef)+ '}'
    ;

memberDef
    : fieldDef
    | funcDeclaration
    ;

fieldDef
    : type name=Identifier ';'
    ;

funcDeclaration returns [ bool isPrivate ]
    : 'private'? { $funcDeclaration::isPrivate=true; } 
      result name=Identifier '(' ( params+=formalParameter (',' params+=formalParameter)* )? ')'
      funcBody
;

result : type |'void';

formalParameter : type name=Identifier;

funcBody : block;

block : '{' blockStatementList? '}';

blockStatementList : statements+=statementInBlock+;

statementInBlock : localVariableDeclaration | statement;

localVariableDeclaration : variableDeclaration ';' ;

variableDeclaration : type name=Identifier ('=' init=expression)? ;

statement
    : token='{' blockStatementList? '}'
    | token=IF '(' expression ')' ifTrue=statement (ELSE ifFalse=statement | { _input.La(1) != ELSE }?)
    | token=FOR '(' forInit? ';' condition=expression? ';' update=expressionList? ')' body=statement
    | token=WHILE '(' expression ')' body=statement
    | token=DO body=statement WHILE '(' expression ')' ';'
    | token=SWITCH '(' expression ')' '{' caseGroup* bottomLabels+=switchLabel* '}'
    | token=RETURN expression? ';'
    | token=BREAK ';'
    | token=CONTINUE ';'
    | statementExpression=expression ';'
    ;

caseGroup : labels=switchLabels stmts=blockStatementList;

switchLabels : labels+=switchLabel labels+=switchLabel*;

switchLabel returns [ bool isDefault ]
    : 'case' constantExpression ':' { $switchLabel::isDefault = false; }
    | 'default' ':'                 { $switchLabel::isDefault = true; }
    ;

constantExpression : literal ;

forInit : expressionList | variableDeclaration;

expressionList : statements+=expression (',' statements+=expression)* ;

primary
    : '(' parenthesized=expression ')' 
    | literal
    | Identifier
    | 'this'
    ;

funcInvocation : neme=Identifier '(' argumentList? ')' ;

argumentList : args+=expression (',' args+=expression)* ;

expression returns [ bool isAssignment ]
    : primary ('.' funcInvocation)?
    | left=expression bop='.' Identifier
    | indexBase=expression '[' index=expression ']'
    | funcInvocation
    | NEW (Identifier '(' ')' | type '[' length=expression ']' )
    | arg=expression postfix=('++' | '--')
    | prefix=('++'|'--') arg=expression
    | prefix='-' arg=expression
    | prefix=('~'|'!') arg=expression
    | left=expression bop=('*'|'/'|'%') right=expression
    | left=expression bop=('+'|'-') right=expression
    | left=expression bop=('<<'|'>>') 
      right=expression
    | left=expression bop=('<=' | '>=' | '>' | '<') right=expression
    | left=expression bop=('==' | '!=') right=expression
    | left=expression bop='&' right=expression
    | left=expression bop='^' right=expression
    | left=expression bop='|' right=expression
    | left=expression bop='&&' right=expression
    | left=expression bop='||' right=expression
    | condition=expression '?' ifTrue=expression ':' ifFalse=expression
    | <assoc=right> left=expression
      bop=('=' | '+=' | '-=' | '*=' | '/=' | '&=' | '|=' | '^=' | '>>=' | '<<=' | '%=')
      right=expression { $expression::isAssignment=true; }
    ;

BOOLEAN : 'boolean';
BREAK : 'break';
CASE : 'case';
CHAR : 'char';
CONTINUE : 'continue';
DO : 'do';
DOUBLE : 'double';
DEFAULT : 'default';
ELSE : 'else';
FLOAT : 'float';
FOR : 'for';
IF : 'if';
INT : 'int';
LONG : 'long';
NEW : 'new';
NULL : 'null';
RETURN : 'return';
CSTRING : 'cstring';
STRUCT : 'struct';
SWITCH : 'switch';
THIS : 'this';
VOID : 'void';
WHILE : 'while';


IntegerLiteral : DecimalNumeral;

fragment
DecimalNumeral: '0' | [1-9][0-9]*;

FloatingPointLiteral : DecimalFloatingPointLiteral;

fragment
DecimalFloatingPointLiteral : DecimalNumeral '.' [0-9]+ ;

BooleanLiteral : 'true' | 'false' ;

CharLiteral : '\'' StringCharacter '\'';

CStringLiteral : '"' StringCharacters? '"';

fragment
StringCharacters : StringCharacter+;

fragment
StringCharacter : ~["'\\\r\n] | EscapeSequence;

fragment
EscapeSequence : '\\' [0tn"'\\];

LPAREN : '(';
RPAREN : ')';
LBRACE : '{';
RBRACE : '}';
LBRACK : '[';
RBRACK : ']';
SEMI : ';';
COMMA : ',';
DOT : '.';



ASSIGN : '=';
GT : '>';
LT : '<';
BANG : '!';
TILDE : '~';
QUESTION : '?';
COLON : ':';
EQUAL : '==';
LE : '<=';
GE : '>=';
NOTEQUAL : '!=';
AND : '&&';
OR : '||';
INC : '++';
DEC : '--';
ADD : '+';
SUB : '-';
MUL : '*';
DIV : '/';
BITAND : '&';
BITOR : '|';
CARET : '^';
MOD : '%';

LSHIFT : '<<';
RSHIFT : '>>';

ADD_ASSIGN : '+=';
SUB_ASSIGN : '-=';
MUL_ASSIGN : '*=';
DIV_ASSIGN : '/=';
AND_ASSIGN : '&=';
OR_ASSIGN : '|=';
XOR_ASSIGN : '^=';
MOD_ASSIGN : '%=';
LSHIFT_ASSIGN : '<<=';
RSHIFT_ASSIGN : '>>=';

AT : '@';
ELLIPSIS : '...';


Identifier : IdentifierLetter IdentifierLetterOrDigit* ;

fragment
IdentifierLetter : [a-zA-Z_];

fragment
IdentifierLetterOrDigit : [a-zA-Z0-9$_];


WS  :  [ \t\r\n\u000C]+ -> skip
    ;

COMMENT
    :   '/*' .*? '*/' -> skip
    ;

LINE_COMMENT
    :   '//' ~[\r\n]* -> skip
    ;