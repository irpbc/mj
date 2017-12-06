using System.Linq;

using mj.compiler.main;
using mj.compiler.utils;
using mj.compiler.resources;

using static mj.compiler.symbol.Scope.LookupKind;

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

        public bool checkUnique(Symbol.MethodSymbol sym, Scope scope)
        {
            bool contains = scope.getSymbolsByName(sym.name, NON_RECURSIVE).Any();
            if (contains) {
                log.error(messages.error_duplicateMethodName, sym.name);
            }
            return !contains;
        }

        public bool checkUnique(Symbol.VarSymbol param, Scope scope)
        {
            Scope.LookupKind lookupKind = NON_RECURSIVE;
            bool contains = scope.getSymbolsByName(param.name, lookupKind).Any();
            if (contains) {
                if (param.kind == Symbol.Kind.PARAM) {
                    log.error(messages.error_duplicateParamName, param.name, scope.owner.name);
                } else {
                    log.error(messages.error_duplicateVar, param.name);
                }
            }
            return !contains;
        }

        public bool checkMainMethod(Symbol main)
        {
            if (main == null || main.kind != Symbol.Kind.MTH) {
                log.error(messages.error_mainMethodNotDefined);
                return false;
            }
            if (main.type.ReturnType != symtab.intType || main.type.ParameterTypes.Count > 0) {
                log.error(messages.error_mainMethodSig);
                return false;
            }
            return false;
        }
    }
}
