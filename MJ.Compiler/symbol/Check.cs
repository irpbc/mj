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

        public bool checkUnique(MethodSymbol sym, Scope scope)
        {
            bool contains = scope.getSymbolsByName(sym.name, LookupKind.NON_RECURSIVE).Any();
            if (contains) {
                log.error(messages.error_duplicateMethodName, sym.name);
            }
            return !contains;
        }

        public bool checkUniqueParam(VarSymbol param, Scope scope)
        {
            if (scope.getSymbolsByName(param.name, LookupKind.NON_RECURSIVE).Any()) {
                log.error(messages.error_duplicateParamName, param.name, scope.owner.name);
                return false;
            }
            return true;
        }

        public bool checkMainMethod(Symbol main)
        {
            if (main == null || main.kind != Kind.MTH) {
                log.error(messages.error_mainMethodNotDefined);
                return false;
            }
            if (main.type.ReturnType != symtab.intType || main.type.ParameterTypes.Count > 0) {
                log.error(messages.error_mainMethodSig);
                return false;
            }
            return false;
        }

        public bool checkUniqueLocalVar(VarSymbol varSymbol, WriteableScope scope)
        {
            if (scope.getSymbolsByName(varSymbol.name, s => s.kind.hasAny(Kind.VAR)).Any()) {
                log.error(messages.error_duplicateVar);
                return false;
            }
            return true;
        }
    }
}
