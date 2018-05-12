# Compiler Manual

## Invoking the compiler

Command line (unpublished .Net Core assembly):

`dotnet MJ.Compiler.dll [options] -o <out_file_name> source files...`

Command line options:
* `-o <output file name>` - name of output object file (should include `.o` extension)
* `--dump-tree` - dump AST as JSON to console
* `--dump-ir` - dump LLVM IR to console

On Windows the `libLLVM.dll` from [MJ runtime library](https://www.github.com/irpbc/mj-rt) should
be present in the program direcotry.

## Linking an executable

Compiler outputs an object file, which has to be linked by a linker. Linking requires the 
[MJ runtime library](https://www.github.com/irpbc/mj-rt) files present in the program directory.

### On Windows

Linking on Windows is done with Microsoft linker (included with VS 2017). Linker should be invoked 
from x64 Native Tools Command Prompt:

`link /out:program.exe program.o mj_rt.lib mj_shim.lib`

You can run the program by entring `program.exe` in the console.

### On Mac OS X

Linking on Mac OS X was tested using the linker supplied with Xcode. It should be invoke like this:

`ld program.o -o program -arch x86_64 -L. -lmj_rt -lmj_shim -lSystem -rpath @executable_path`

Details
* `-arch x86_64` is apperently required for the linker to properly find the main function.
* `-L.` tell the linker to look for libraries in the current directory.
* `-lmj_rt -lmj_shim` linkes the runtime lib and the static glue code.
* `-lSystem` linkes in `libSystem.dylib` which is required.
* `-rpath @executable_path` specifies to the runtime loader to search for libraries in the executable
  directory (which we need if the `libmj_rt.dylib` is located there; Windows searches the executable
  dir by default, so we dont't supply any such options there).

You can run the program by entering `./program` in the console.