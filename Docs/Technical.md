## Technical Docs

### Data structures

There are several main types of objects in the compiler:

* [`Trees`](../MJ.Compiler/parsing/ast/Tree.cs) - They represent a program as and AST. 
When parsed, the nodes have only the synactic information. They are later augmented 
with semantic information.
* [`Symbols`](../MJ.Compiler/symbol/Symbol.cs) - They hold semantic information about 
the program (mostly about types).
    * `MethodSymbol` - Contains semantic info about methods (name, signature).
    * `VarSymbol` - Contains semantic info about variables (name, type).
    * `TypeSymbol` - Abstract class for type symbols, with only one concrete subclass,
    the `PrimitiveTypeSymbol`, which are predefined.
    * `OperatorSymbol` - These are predefined symbols for every operator (one for each
    allowed combination of operand types)
    * `TopLevelSymbol` - A "singleton" symbol for the whole program. (might be unnecesery)
* [`Types`](../MJ.Compiler/symbol/Type.cs) - Type information. With subclasses:
    * `PrimitiveType` - A subclass for primitive types.
    * `MethodType` - A type that holds the signature for a method (types for arguments,
    and type of return value)
* [`Scope`](../MJ.Compiler/symbol/Scope.cs) - Represents a syntactic scope. Used during
semantic analysis.


### Parsing

Parsing is done with ANTLR4. The compiler is using the [C# port](https://github.com/tunnelvisionlabs/antlr4cs) 
of ANTLR4 by [Sam Harwell](https://github.com/sharwell) (working at Microsoft as of this writting).

The actual parser is generated using a [`grammer file`](../MJ.Compiler/antlrgrammar/MJ.g4). 
This parser generates a parse tree consisting of ANTLR generated classes, which maps one-to-one 
to the grammer, and as such, it is not suitable to be used in the later stages of compilation.

This parse tree is thus converted into an [`Abstract Syntax Tree`](../MJ.Compiler/tree/Tree.cs)
using an ANTLR visitor ([`AstGeneratingParseTreeVisitor`](../MJ.Compiler/parsing/AstGeneratingParseTreeVisitor.cs))
which contains boilerplate code to convert grammar constructs into AST constructs.


### Semantic analysis logic

Logic of semantic analysis is contained in the following classes:

* [`DeclarationAnalysis`](../MJ.Compiler/symbol/DeclarationAnalysis.cs) - Responsible for 
creating `Symbols` for declarations (methods and parameters) from `Trees` and entering them 
into their enclosing scopes. Enters methods into the top level scope, parameters into the  
method scope.
* [`TypeAnalysis`](../MJ.Compiler/symbol/CodeAnalysis.cs) - Responsible for resolving types, 
methods calls, operators , binding variables to indentifier etc., ensures there are no
duplicate variables inside method bodies, using scopes and types.
* [`FlowAnalysys`](../MJ.Compiler/symbol/FlowAnalysis.cs) - Responsible for ensuring there
is no unreachable code and that every variable is assigned at any point of use.
* [`Operators`](../MJ.Compiler/symbol/Operators.cs) - Contains predefined operator symbols
and operator resolution logic.
* [`Symtab`](../MJ.Compiler/symbol/Symtab.cs) - Contains predefined symbols for primitive types
as well as some utility symbols used during analysis (error types and error symbols).


### Executable code generation

For code generation the compiler uses LLVM via [LLVMSharp](https://github.com/Microsoft/LLVMSharp)
bindings.

Important classes:
* [`CodeGenerator`](../MJ.Compiler/codegen/CodeGenerator.cs) - Code generation visitor.
* [`LLVMTypeResolver`](../MJ.Compiler/codegen/LLVMTypeResolver.cs) - Returns LLVM types for MJ types.
* [`VariableAllocator`](../MJ.Compiler/codegen/VariableAllocator.cs) - Gathers all local variables to 
declare them at the tom of the function.

#### Code generation techniques

All local variables are predeclared at the top of the function using `alloca` instructions, as per the
recomendation of the LLVM docs. Later, load and store instructions are generated when accessing variables.
LLVM then runs an algorithm to remove `allocas` and convert the variables to SSA form. Clang itself 
uses this technique to generate code. To support reassigning of function parameters, after declaring
`allocas` for local variables, additional `allocas` are declare for each parameter, and param values
loaded into them. These are also eliminated by LLVM.

#### Garbage collection

For accurate garbage collection compiler emits constant data with information about objects in
the program (length of objects and offsets of fields with heap pointers). Pointer to this info
is passed when allocating objects and arrays, and is stored in the header of every object for
GC to use to accurately identify all pointers inside objects.
