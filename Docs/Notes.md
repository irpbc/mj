## Some notes by developer

The Javac compiler's source code was the inspiration for the organization and data 
structures used in this compiler.

From looking at the bytecode generation part of the java compiler it seems that it is way 
easier to generate LLVM IR using the LLVM api than JVM bytecode for method bodies for the
usual imperative languages with familiar expressions and control flow statements.
C# was chosen for this project because of better interop with native libraries (LLVM in
this case).
