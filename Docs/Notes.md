# Some notes by developer

## Architecture

The Javac compiler's source code was the inspiration for the organization and data 
structures used in this compiler.

## Choice of LLVM and C#

From looking at the bytecode generation part of the java compiler it seems that it is way 
easier to generate LLVM IR using the LLVM api than JVM bytecode for method bodies for the
usual imperative languages with familiar expressions and control flow statements.
C# was chosen for this project because of better interop with native libraries (LLVM in
this case).

### AOP implementation

TODO

### Garbage collection

It turned out implementing a rudimentary accurate GC was not relly dificult. The hardest
part to get right is stack walking. It was a lot of trial and error to discover locations
of return addresses. I found that you can't get a pointer to the previous stack frame
from the current, and must use the LLVM generated stack map which gives you the size of 
the stack frame at the current return address. This required the use of couple of assembly
instractions as a trampoline to extract the stack pointer from RSP register when calling
memory allocation functions. This assembly is different depending of OS and assambler
syntax. The second part is extracting the stack maps from a section in the EXE file. This
step is also OS dependent. Once we can reliably walk the stack and read heap pointer
locations from the stack maps, everything else is just a matter of implementing the chosen
garbage collection method in C++ and making the compiler leave metadata for the GC to pick
up and use inside the passes.
