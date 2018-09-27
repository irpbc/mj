using System.Linq;

using mj.compiler.main;
using mj.compiler.resources;
using mj.compiler.utils;

using static mj.compiler.symbol.Scope;
using static mj.compiler.symbol.Symbol;

namespace mj.compiler.symbol
{
    public class Check
    {
        private static readonly Context.Key<Check> CONTEXT_KEY = new Context.Key<Check>();

        public static Check instance(Context ctx) =>
            ctx.tryGet(CONTEXT_KEY, out var instance) ? instance : new Check(ctx);

        private readonly Log log;
        private readonly Symtab symtab;

        private Check(Context ctx)
        {
            ctx.put(CONTEXT_KEY, this);

            log = Log.instance(ctx);
            symtab = Symtab.instance(ctx);
        }

        public bool checkUnique(DiagnosticPosition pos, FuncSymbol sym, Scope scope)
        {
            bool contains = scope.getSymbolsByName(sym.name, LookupKind.NON_RECURSIVE).Any();
            if (contains) {
                log.error(pos, messages.duplicateFunctionName, sym.name);
            }
            return !contains;
        }

        public bool checkUniqueParam(DiagnosticPosition pos, VarSymbol param, Scope scope)
        {
            if (scope.getSymbolsByName(param.name, LookupKind.NON_RECURSIVE).Any()) {
                log.error(pos, messages.duplicateParamName, param.name, scope.owner.name);
                return false;
            }
            return true;
        }

        public bool checkMainFunction(DiagnosticPosition pos, FuncSymbol main)
        {
            // Mimic C main function sig: int main(int,char**)
            // with long substituting pointer (implying 64bit arch)
            if (main.type.ReturnType != symtab.intType || 
                main.type.ParameterTypes.Count != 2 || 
                main.type.ParameterTypes[0] != symtab.intType || 
                main.type.ParameterTypes[1] != symtab.longType) {
                log.error(pos, messages.mainFunctionSig);
                return false;
            }
            return true;
        }

        public bool checkUniqueLocalVar(DiagnosticPosition pos, VarSymbol varSymbol, WriteableScope scope)
        {
            if (scope.getSymbolsByName(varSymbol.name, s => (s.kind & Kind.VAR) != 0).Any()) {
                log.error(pos, messages.duplicateVar, varSymbol.name);
                return false;
            }
            return true;
        }

        public bool checkUnique(DiagnosticPosition pos, StructSymbol ssym, WriteableScope scope)
        {
            bool contains = scope.getSymbolsByName(ssym.name, LookupKind.NON_RECURSIVE).Any();
            if (contains) {
                log.error(pos, messages.duplicateStructName, ssym.name);
            }
            return !contains;
        }
    }
}
