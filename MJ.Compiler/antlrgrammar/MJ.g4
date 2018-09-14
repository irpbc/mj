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
	|   StringLiteral        { $literal::literalType=STRING; }
	|   NULL                 { $literal::literalType=NULL; }
    ;

type returns [ int arrays ]
	:	(primitive='int'
    |	primitive='long'
    |	primitive='float'
    |	primitive='double'
    |	primitive='boolean'
    |	primitive='string'
    |   className=Identifier) ('[' ']' { $type::arrays++; } )*
	;

compilationUnit
	:   declarations+=declaration+ EOF
	;
	
declaration
    :   classDef
    |   methodDeclaration
//    |   aspectDef
    ;

/*annotation
    :   '@' name=Identifier
    ;*/
    
classDef
    : 'struct' name=Identifier '{' (fields+=fieldDef)+ '}'
    ;

fieldDef
    : type name=Identifier ';'
    ;

methodDeclaration returns [ bool isPrivate ]
	:	/*annotations+=annotation**/
	    'private'? { $methodDeclaration::isPrivate=true; } 
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

/*aspectDef
    : 'aspect' name=Identifier '{' 
          ( afterStart='after' after=block )?
      '}'
    ;*/

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
   :   token='{' blockStatementList? '}'
   |   token=IF '(' expression ')' ifTrue=statement (ELSE ifFalse=statement | { _input.La(1) != ELSE }?)
   |   token=FOR '(' forInit? ';' condition=expression? ';' update=expressionList? ')' body=statement
   |   token=WHILE '(' expression ')' body=statement
   |   token=DO body=statement WHILE '(' expression ')' ';'
   |   token=SWITCH '(' expression ')' '{' caseGroup* bottomLabels+=switchLabel* '}'
   |   token=RETURN expression? ';'
   |   token=BREAK ';'
   |   token=CONTINUE ';'
   |   statementExpression=expression ';'
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

forInit
	:	expressionList
	|	variableDeclaration
	;

expressionList
	:	statements+=expression (',' statements+=expression)*
	;

primary
	:   '(' parenthesized=expression ')' 
	|   literal
	|   Identifier
	;

methodInvocation
	:	neme=Identifier '(' argumentList? ')'
	;

argumentList
	:	args+=expression (',' args+=expression)*
	;

expression returns [ bool isAssignment, int shiftOp ]
    : primary
    | left=expression bop='.' Identifier
    | indexBase=expression '[' index=expression ']'
    | methodInvocation
    | NEW (Identifier '(' ')' | type '[' length=expression ']' )
    | arg=expression postfix=('++' | '--')
    | prefix=('++'|'--') arg=expression
    | prefix='-' arg=expression
    | prefix=('~'|'!') arg=expression
    | left=expression bop=('*'|'/'|'%') right=expression
    | left=expression bop=('+'|'-') right=expression
    | left=expression ('<' '<' { $expression::shiftOp=LSHIFT; } | '>' '>' { $expression::shiftOp=RSHIFT; }) 
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
STRING : 'string';
STRUCT : 'struct';
SWITCH : 'switch';
VOID : 'void';
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

StringLiteral
	:	'"' StringCharacters? '"'
	;
fragment
StringCharacters
	:	StringCharacter+
	;
fragment
StringCharacter
	:	~["\\\r\n]
	|	EscapeSequence
	;
// §3.10.6 Escape Sequences for Character and String Literals
fragment
EscapeSequence
	:	'\\' [btnfr"'\\]
    |   UnicodeEscape // This is not in the spec but prevents having to preprocess the input
	;

// This is not in the spec but prevents having to preprocess the input
fragment
UnicodeEscape
    :   '\\' 'u'+  HexDigit HexDigit HexDigit HexDigit
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



Identifier
	:	IdentifierLetter IdentifierLetterOrDigit*
	;

fragment
IdentifierLetter
	:	[a-zA-Z$_] // these are the "java letters" below 0x7F
	|	// covers all characters above 0x7F which are not a surrogate
		~[\u0000-\u007F\uD800-\uDBFF]
		{Utils.IsIdentifierStart((char)_input.La(-1))}?
	//|	// covers UTF-16 surrogate pairs encodings for U+10000 to U+10FFFF
	//	[\uD800-\uDBFF] [\uDC00-\uDFFF]
	//	{Utils.IsIdentifierStart(Char.ConvertToUtf32((char)_input.La(-2), (char)_input.La(-1)))}?
	;

fragment
IdentifierLetterOrDigit
	:	[a-zA-Z0-9$_] // these are the "java letters or digits" below 0x7F
	|	// covers all characters above 0x7F which are not a surrogate
		~[\u0000-\u007F\uD800-\uDBFF]
		{Utils.IsIdentifierPart((char)_input.La(-1))}?
	//|	// covers UTF-16 surrogate pairs encodings for U+10000 to U+10FFFF
	//	[\uD800-\uDBFF] [\uDC00-\uDFFF]
	//	{Utils.IsIdentifierPart(Char.ConvertToUtf32((char)_input.La(-2), (char)_input.La(-1)))}?
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