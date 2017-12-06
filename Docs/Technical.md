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

### Semantic analysis logic

Logic of semantic analysis is contained in the following classes:

* [`DeclarationAnalysis`](../MJ.Compiler/symbol/DeclarationAnalysis.cs) - Responsible for 
creating `Symbols` for declarations (methods and parameters) from `Trees` and entering them 
into their enclosing scopes. Enters methods into the top level scope, parameters into the  
method scope.
* [`CodeAnalysis`](../MJ.Compiler/symbol/CodeAnalysis.cs) - Responsible for analyzing the 
method bodies, resolving types methods, local variables, operators etc.