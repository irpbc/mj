# MJ Compiler

A compiler for a simple C-like programming language (a college final project!).

## Syntax and semantics

Language is based on widely used C syntax. It's syntax supports:

* Functions (refered to as "methods" in the compiler)
* Usual control flow statements (`if`, `for`, `while`, `do`, `switch`)
* Variable declarations
* Primitive types: `int`, `long`, `float`, `double` and `boolean`
* Comments (single line `// ...` and block `/* ... */`)
* `string` type, which is a pointer to a null terminated utf8 string, and currently 
does not support any operations except creating one with a string literal
* Declaring structured types, which are heap allocated and garbage collected.

The language has some built-in functions:

* `void hello()` - prints "Hello World"
* `int puts(string str)` - prints a string to the console
* `int printf(string format, ...)` - a binding for standard C `printf` function
(with varargs).
* `scan_*()` functions (specialized bindings to `scanf`):
    * `int scan_int()` - read an `int` from console
    * `long scan_long()` - read a `long` from console
    * `float scan_float()` - read a `float` from console
    * `double scan_double()` - read a `double` from console


It **does not** support (for now):

* Arrays

### Example

```
int fib(int n) {
    if (n < 2) {
        return 1;
    }
    int fib1 = fib(n-1);
    int fib2 = fib(n-2);
    return fib1 + fib2;
}

int main(int argc, long argvPtr) {
    return fib(12);
}

// main signature mimics the C signature "int main(int argc, char* argv[])" 
// with a long substituted for "char**" pointer (assumed 64 bits in size)
```

## Technical info

Compiler is written in C# 7 (.Net Core), parser is generated with ANTLR4, and uses LLVM for code 
generation.

The runtime library and compiler native code is located at [irpbc/mj-rt](http://github.com/irpbc/mj-rt)

[Technocal docs](Docs/Technical.md)

## Manual

[The compiler manual](Docs/Manual.md)