using System.Collections.Generic;
using System.Linq;

using mj.compiler.main;
using mj.compiler.utils;
using mj.compiler.resources;

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

        public bool checkUnique(DiagnosticPosition pos, MethodSymbol sym, Scope scope)
        {
            bool contains = scope.getSymbolsByName(sym.name, LookupKind.NON_RECURSIVE).Any();
            if (contains) {
                log.error(pos, messages.duplicateMethodName, sym.name);
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

        public bool checkMainMethod(DiagnosticPosition pos, MethodSymbol main)
        {
            if (main.type.ReturnType != symtab.intType || main.type.ParameterTypes.Count > 0) {
                log.error(pos, messages.mainMethodSig);
                return false;
            }
            return false;
        }

        public bool checkUniqueLocalVar(DiagnosticPosition pos, VarSymbol varSymbol, WriteableScope scope)
        {
            if (scope.getSymbolsByName(varSymbol.name, s => s.kind.hasAny(Kind.VAR)).Any()) {
                log.error(pos, messages.duplicateVar);
                return false;
            }
            return true;
        }
    }
}
