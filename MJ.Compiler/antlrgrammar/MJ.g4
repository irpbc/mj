grammar MJ;

@header {
using System;
}

@members {
    //public enum LiteralType { INTEGER, FLOAT, BOOLEAN }
}

literal returns [ int literalType ]
	:	IntegerLiteral       { $literal::literalType=INT; }
	|	FloatingPointLiteral { $literal::literalType=FLOAT; }
	|	BooleanLiteral       { $literal::literalType=BOOLEAN; }
    ;

type
	:	primitive='int'
    |	primitive='long'
    |	primitive='float'
    |	primitive='double'
    |	primitive='boolean'
	;

nameExpression
	:	Identifier
	;

compilationUnit
	:   methods+=methodDeclaration+	EOF
	;

methodDeclaration returns [ bool isPrivate ]
	:	'private'? { $methodDeclaration::isPrivate=true; } 
	    result 
	    name=Identifier '(' ( params+=formalParameter (',' params+=formalParameter)* )? ')'
	    methodBody
	;

result
	:	type
	|	'void'
	;

formalParameter
	:	type name=Identifier
	;

methodBody
	:	block
	;

block
	:	'{' blockStatementList? '}'
	;

blockStatementList
	:	statements+=statementInBlock+
	;

statementInBlock
	:	localVariableDeclaration
	|	statement
	;

localVariableDeclaration
    :   variableDeclaration ';'
    ;

variableDeclaration
	:	type name=Identifier ('=' init=expression)?
	;

statement
	:	statementWithoutTrailingSubstatement
	|	ifThenStatement
	|	ifThenElseStatement
	|	whileStatement
	|	forStatement
	;

statementNoShortIf
	:	statementWithoutTrailingSubstatement
	|	ifThenElseStatementNoShortIf
	|	whileStatementNoShortIf
	|	forStatementNoShortIf
	;

statementWithoutTrailingSubstatement
	:	block
	|	expressionStatement
	|	switchStatement
	|	doStatement
	|	breakStatement
	|	continueStatement
	|	returnStatement
	;

expressionStatement
	:	statementExpression ';'
	;

statementExpression
	:	assignment
	|	preIncDecExpression
	|	postIncDecExpression
	|	methodInvocation
	;

ifThenStatement
	:	'if' '(' condition=expression ')' ifTrue=statement
	;

ifThenElseStatement
	:	'if' '(' condition=expression ')' ifTrue=statementNoShortIf 'else' ifFalse=statement
	;

ifThenElseStatementNoShortIf
	:	'if' '(' condition=expression ')' ifTrue=statementNoShortIf 'else' ifFalse=statementNoShortIf
	;

switchStatement
	:	'switch' '(' expression ')' '{' caseGroup* bottomLabels+=switchLabel* '}'
	;

caseGroup
	:	labels=switchLabels stmts=blockStatementList
	;

switchLabels
	:	labels+=switchLabel labels+=switchLabel*
	;

switchLabel returns [ bool isDefault ]
	:	'case' constantExpression ':' { $switchLabel::isDefault = false; }
	|	'default' ':'                 { $switchLabel::isDefault = true; }
	;

constantExpression
    : literal
    ;

whileStatement
	:	'while' '(' condition=expression ')' statement
	;

whileStatementNoShortIf
	:	'while' '(' condition=expression ')' statementNoShortIf
	;

doStatement
	:	'do' statement 'while' '(' condition=expression ')' ';'
	;

forStatementNoShortIf
	:	'for' '(' forInit? ';' condition=expression? ';' update=forUpdate? ')' statementNoShortIf
	;

forStatement
	:	'for' '(' forInit? ';' condition=expression? ';' update=forUpdate? ')' statement
	;

forInit
	:	statementExpressionList
	|	variableDeclaration
	;

forUpdate
	:	statementExpressionList
	;

statementExpressionList
	:	statements+=statementExpression (',' statements+=statementExpression)*
	;

breakStatement
	:	'break' ';'
	;

continueStatement
	:	'continue' ';'
	;

returnStatement
	:	'return' value=expression? ';'
	;


primary
	:	literal
	|	'(' parenthesized=expression ')'
	|	methodInvocation
	;



methodInvocation
	:	neme=Identifier '(' argumentList? ')'
	;


argumentList
	:	args+=expression (',' args+=expression)*
	;


expression
	:	conditionalExpression
	|	assignment
	;

assignment
	:	leftHandSide assignmentOperator expression
	;

leftHandSide
	:	nameExpression
	;

assignmentOperator
	:	op='='
	|	op='*='
	|	op='/='
	|	op='%='
	|	op='+='
	|	op='-='
	|	op='<<='
	|	op='>>='
	|	op='&='
	|	op='^='
	|	op='|='
	;

conditionalExpression
	:	down=conditionalOrExpression
	|	condition=conditionalOrExpression '?' ifTrue=expression ':' ifFalse=conditionalExpression
	;

conditionalOrExpression
	:	down=conditionalAndExpression
	|	left=conditionalOrExpression operator='||' right=conditionalAndExpression
	;

conditionalAndExpression
	:	down=inclusiveOrExpression
	|	left=conditionalAndExpression operator='&&' right=inclusiveOrExpression
	;

inclusiveOrExpression
	:	down=exclusiveOrExpression
	|	left=inclusiveOrExpression operator='|' right=exclusiveOrExpression
	;

exclusiveOrExpression
	:	down=andExpression
	|	left=exclusiveOrExpression operator='^' right=andExpression
	;

andExpression
	:	down=equalityExpression
	|	left=andExpression operator='&' right=equalityExpression
	;

equalityExpression
	:	down=relationalExpression
	|	left=equalityExpression operator='==' right=relationalExpression
	|	left=equalityExpression operator='!=' right=relationalExpression
	;

relationalExpression
	:	down=shiftExpression
	|	left=relationalExpression operator='<' right=shiftExpression
	|	left=relationalExpression operator='>' right=shiftExpression
	|	left=relationalExpression operator='<=' right=shiftExpression
	|	left=relationalExpression operator='>=' right=shiftExpression
	;

shiftExpression returns [ int operator ]
	:	down=additiveExpression
	|	left=shiftExpression '<' '<' right=additiveExpression {$shiftExpression::operator = LSHIFT;}
	|	left=shiftExpression '>' '>' right=additiveExpression {$shiftExpression::operator = RSHIFT;}
	;

additiveExpression
	:	down=multiplicativeExpression
	|	left=additiveExpression operator='+' right=multiplicativeExpression
	|	left=additiveExpression operator='-' right=multiplicativeExpression
	;

multiplicativeExpression
	:	down=unaryExpression
	|	left=multiplicativeExpression operator='*' right=unaryExpression
	|	left=multiplicativeExpression operator='/' right=unaryExpression
	|	left=multiplicativeExpression operator='%' right=unaryExpression
	;

unaryExpression
	:	operator='++' arg=unaryExpression
	|	operator='--' arg=unaryExpression
	|	operator='+' arg=unaryExpression
	|	operator='-' arg=unaryExpression
	|	down=unaryExpressionNotPlusMinus
	;

preIncDecExpression
	:	operator=('++' | '--' ) arg=unaryExpression
	;

unaryExpressionNotPlusMinus
	:	down=postfixExpression
	|	operator='~' arg=unaryExpression
	|	operator='!' arg=unaryExpression
	;

postfixExpression
	:	( down=primary | nameExpression )
		( postfixes+='++' | postfixes+='--' )*
	;

postIncDecExpression
	:	arg=postfixExpression operator=('++' | '--' )
	;

ABSTRACT : 'abstract';
ASSERT : 'assert';
BOOLEAN : 'boolean';
BREAK : 'break';
BYTE : 'byte';
CASE : 'case';
CATCH : 'catch';
CHAR : 'char';
CLASS : 'class';
CONST : 'const';
CONTINUE : 'continue';
DEFAULT : 'default';
DO : 'do';
DOUBLE : 'double';
ELSE : 'else';
ENUM : 'enum';
EXTENDS : 'extends';
FINAL : 'final';
FINALLY : 'finally';
FLOAT : 'float';
FOR : 'for';
IF : 'if';
GOTO : 'goto';
IMPLEMENTS : 'implements';
IMPORT : 'import';
INSTANCEOF : 'instanceof';
INT : 'int';
INTERFACE : 'interface';
LONG : 'long';
NATIVE : 'native';
NEW : 'new';
PACKAGE : 'package';
PRIVATE : 'private';
PROTECTED : 'protected';
PUBLIC : 'public';
RETURN : 'return';
SHORT : 'short';
STATIC : 'static';
STRICTFP : 'strictfp';
SUPER : 'super';
SWITCH : 'switch';
SYNCHRONIZED : 'synchronized';
THIS : 'this';
THROW : 'throw';
THROWS : 'throws';
TRANSIENT : 'transient';
TRY : 'try';
VOID : 'void';
VOLATILE : 'volatile';
WHILE : 'while';



IntegerLiteral
	:	DecimalIntegerLiteral
	|	HexIntegerLiteral
	|	OctalIntegerLiteral
	|	BinaryIntegerLiteral
	;

fragment
DecimalIntegerLiteral
	:	DecimalNumeral IntegerTypeSuffix?
	;

fragment
HexIntegerLiteral
	:	HexNumeral IntegerTypeSuffix?
	;

fragment
OctalIntegerLiteral
	:	OctalNumeral IntegerTypeSuffix?
	;

fragment
BinaryIntegerLiteral
	:	BinaryNumeral IntegerTypeSuffix?
	;

fragment
IntegerTypeSuffix
	:	[lL]
	;

fragment
DecimalNumeral
	:	'0'
	|	NonZeroDigit (Digits? | Underscores Digits)
	;

fragment
Digits
	:	Digit (DigitsAndUnderscores? Digit)?
	;

fragment
Digit
	:	'0'
	|	NonZeroDigit
	;

fragment
NonZeroDigit
	:	[1-9]
	;

fragment
DigitsAndUnderscores
	:	DigitOrUnderscore+
	;

fragment
DigitOrUnderscore
	:	Digit
	|	'_'
	;

fragment
Underscores
	:	'_'+
	;

fragment
HexNumeral
	:	'0' [xX] HexDigits
	;

fragment
HexDigits
	:	HexDigit (HexDigitsAndUnderscores? HexDigit)?
	;

fragment
HexDigit
	:	[0-9a-fA-F]
	;

fragment
HexDigitsAndUnderscores
	:	HexDigitOrUnderscore+
	;

fragment
HexDigitOrUnderscore
	:	HexDigit
	|	'_'
	;

fragment
OctalNumeral
	:	'0' Underscores? OctalDigits
	;

fragment
OctalDigits
	:	OctalDigit (OctalDigitsAndUnderscores? OctalDigit)?
	;

fragment
OctalDigit
	:	[0-7]
	;

fragment
OctalDigitsAndUnderscores
	:	OctalDigitOrUnderscore+
	;

fragment
OctalDigitOrUnderscore
	:	OctalDigit
	|	'_'
	;

fragment
BinaryNumeral
	:	'0' [bB] BinaryDigits
	;

fragment
BinaryDigits
	:	BinaryDigit (BinaryDigitsAndUnderscores? BinaryDigit)?
	;

fragment
BinaryDigit
	:	[01]
	;

fragment
BinaryDigitsAndUnderscores
	:	BinaryDigitOrUnderscore+
	;

fragment
BinaryDigitOrUnderscore
	:	BinaryDigit
	|	'_'
	;



FloatingPointLiteral
	:	DecimalFloatingPointLiteral
	|	HexadecimalFloatingPointLiteral
	;

fragment
DecimalFloatingPointLiteral
	:	Digits '.' Digits? ExponentPart? FloatTypeSuffix?
	|	'.' Digits ExponentPart? FloatTypeSuffix?
	|	Digits ExponentPart FloatTypeSuffix?
	|	Digits FloatTypeSuffix
	;

fragment
ExponentPart
	:	ExponentIndicator SignedInteger
	;

fragment
ExponentIndicator
	:	[eE]
	;

fragment
SignedInteger
	:	Sign? Digits
	;

fragment
Sign
	:	[+-]
	;

fragment
FloatTypeSuffix
	:	[fFdD]
	;

fragment
HexadecimalFloatingPointLiteral
	:	HexSignificand BinaryExponent FloatTypeSuffix?
	;

fragment
HexSignificand
	:	HexNumeral '.'?
	|	'0' [xX] HexDigits? '.' HexDigits
	;

fragment
BinaryExponent
	:	BinaryExponentIndicator SignedInteger
	;

fragment
BinaryExponentIndicator
	:	[pP]
	;



BooleanLiteral
	:	'true'
	|	'false'
	;


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

LSHIFT : 'LSHIFT';
RSHIFT : 'RSHIFT';

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



Identifier
	:	IdentifierLetter IdentifierLetterOrDigit*
	;

fragment
IdentifierLetter
	:	[a-zA-Z$_] // these are the "java letters" below 0x7F
	|	// covers all characters above 0x7F which are not a surrogate
		~[\u0000-\u007F\uD800-\uDBFF]
		{Utils.IsIdentifierStart(_input.La(-1))}?
	|	// covers UTF-16 surrogate pairs encodings for U+10000 to U+10FFFF
		[\uD800-\uDBFF] [\uDC00-\uDFFF]
		{Utils.IsIdentifierStart(Char.ConvertToUtf32((char)_input.La(-2), (char)_input.La(-1)))}?
	;

fragment
IdentifierLetterOrDigit
	:	[a-zA-Z0-9$_] // these are the "java letters or digits" below 0x7F
	|	// covers all characters above 0x7F which are not a surrogate
		~[\u0000-\u007F\uD800-\uDBFF]
		{Utils.IsIdentifierPart(_input.La(-1))}?
	|	// covers UTF-16 surrogate pairs encodings for U+10000 to U+10FFFF
		[\uD800-\uDBFF] [\uDC00-\uDFFF]
		{Utils.IsIdentifierPart(Char.ConvertToUtf32((char)_input.La(-2), (char)_input.La(-1)))}?
	;



AT : '@';
ELLIPSIS : '...';



WS  :  [ \t\r\n\u000C]+ -> skip
    ;

COMMENT
    :   '/*' .*? '*/' -> skip
    ;

LINE_COMMENT
    :   '//' ~[\r\n]* -> skip
    ;