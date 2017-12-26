## MJ Compiler

A compiler for a simple C-like programming language.

### Syntax and semantics

Language is based on widely used C syntax. It's syntax supports:

* Functions (refered to as "methods" in the compiler)
* Usual control flow statements (`if`, `for`, `while`, `do`, `switch`)
* Variable declarations
* Types: `int`, `long`, `float`, `double` and `boolean`

It **does not** support (for now):

* Comments
* Any kind of character or string type
* Arrays
* Aggregate types (like structs or objects)

#### Example

```
int fib(int n) {
    if (n < 2) {
        return 1;
    }
    int fib1 = fib(n-1);
    int fib2 = fib(n-2);
    return fib1 + fib2;
}

int main() {
    return fib(12);
}
```

### Technical

Compiler is written in C# 7, parser is generated with ANTLR4, and uses LLVM for code 
generation.