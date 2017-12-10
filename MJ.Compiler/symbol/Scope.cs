using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Security;

using mj.compiler.utils;

using Newtonsoft.Json;

namespace mj.compiler.symbol
{
    public abstract class Scope
    {
        [JsonIgnore]
        public readonly Symbol owner;

        protected Scope(Symbol owner)
        {
            this.owner = owner;
        }

        public delegate bool Filter<in T>(T t);

        public static readonly Filter<Object> NO_FILTER = o => true;

        /// Returns all Symbols in this Scope. Symbols from outward Scopes are included
        /// iff lookupKind == RECURSIVE.
        public IEnumerable<Symbol> getSymbols(LookupKind lookupKind = LookupKind.RECURSIVE)
        {
            return getSymbols(NO_FILTER, lookupKind);
        }

        /// Returns Symbols that match the given filter. Symbols from outward Scopes are included
        /// iff lookupKind == RECURSIVE.
        public abstract IEnumerable<Symbol> getSymbols(Filter<Symbol> sf, LookupKind lookupKind = LookupKind.RECURSIVE);

        /// Returns Symbols with the given name. Symbols from outward Scopes are included
        /// iff lookupKind == RECURSIVE.
        public IEnumerable<Symbol> getSymbolsByName(String name, LookupKind lookupKind = LookupKind.RECURSIVE)
        {
            return getSymbolsByName(name, NO_FILTER, lookupKind);
        }

        /// Returns Symbols with the given name that match the given filter.
        /// Symbols from outward Scopes are included iff lookupKind == RECURSIVE.
        public abstract IEnumerable<Symbol> getSymbolsByName(String name, Filter<Symbol> sf,
                                                             LookupKind lookupKind = LookupKind.RECURSIVE);

        /// Return the first Symbol from this or outward scopes with the given name.
        /// Returns null if none.
        public Symbol findFirst(String name)
        {
            return findFirst(name, NO_FILTER);
        }

        /// Return the first Symbol from this or outward scopes with the given name that matches the
        /// given filter. Returns null if none.
        public Symbol findFirst(String name, Filter<Symbol> sf)
        {
            return getSymbolsByName(name, sf).FirstOrDefault();
        }

        /// Returns true iff there are is at least one Symbol in this scope matching the given filter.
        /// Does not inspect outward scopes.
        public bool anyMatch(Filter<Symbol> filter)
        {
            return getSymbols(filter, LookupKind.NON_RECURSIVE).Any();
        }

        /// Returns true iff the given Symbol is in this scope, optionally checking outward scopes.
        public bool includes(Symbol sym, LookupKind lookupKind = LookupKind.RECURSIVE)
        {
            return getSymbolsByName(sym.name, t => t == sym, lookupKind).Any();
        }

        /// Returns true iff this scope does not contain any Symbol. Does not inspect outward scopes.
        public bool isEmpty()
        {
            return !getSymbols(LookupKind.NON_RECURSIVE).Any();
        }

        public enum LookupKind
        {
            RECURSIVE,
            NON_RECURSIVE
        }

        public abstract class WriteableScope : Scope
        {
            protected WriteableScope(Symbol owner) : base(owner) { }

            /// Enter the given Symbol into this scope.
            public abstract void enter(Symbol s);

            /// Enter symbol sym in this scope if not already there.
            public abstract void enterIfAbsent(Symbol c);

            public abstract void remove(Symbol c);

            /// Construct a fresh scope within this scope, with same owner. The new scope may
            /// shares internal structures with the this scope. Used in connection with
            /// method leave if scope access is stack-like in order to avoid allocation
            /// of fresh tables.
            public WriteableScope subScope() => subScope(this.owner);

            /// Construct a fresh scope within this scope, with new owner. The new scope may
            /// shares internal structures with the this scope. Used in connection with
            /// method leave if scope access is stack-like in order to avoid allocation
            /// of fresh tables.
            public abstract WriteableScope subScope(Symbol newOwner);

            /// Must be called on dup-ed scopes to be able to work with the outward scope again.
            public abstract WriteableScope leave();

            /// Create a new WriteableScope.
            public static WriteableScope create(Symbol owner) => new ScopeImpl(owner);
        }

        private class ScopeImpl : WriteableScope
        {
            [JsonIgnore]
            private WriteableScope outer;

            public ScopeImpl(Symbol owner, WriteableScope outer = null) : base(owner)
            {
                this.outer = outer;
            }

            private readonly Dictionary<String, Symbol> dict = new Dictionary<String, Symbol>();

            public override void enter(Symbol s) => dict.Add(s.name, s);

            public override void enterIfAbsent(Symbol c)
            {
                if (!dict.ContainsKey(c.name)) {
                    dict.Add(c.name, c);
                }
            }

            public override void remove(Symbol c) => dict.Remove(c.name);

            public override WriteableScope subScope(Symbol newOwner) => new ScopeImpl(newOwner, this);
            public override WriteableScope leave() => outer;

            public override IEnumerable<Symbol> getSymbols(Filter<Symbol> sf,
                                                           LookupKind lookupKind = LookupKind.RECURSIVE)
            {
                IEnumerable<Symbol> enumerable = dict.Values.Where(new Func<Symbol, bool>(sf));
                if (lookupKind is LookupKind.NON_RECURSIVE || outer is null) {
                    return enumerable;
                }
                return enumerable.Concat(outer.getSymbols(sf));
            }

            public override IEnumerable<Symbol> getSymbolsByName(String name, Filter<Symbol> sf,
                                                                 LookupKind lookupKind = LookupKind.RECURSIVE)
            {
                IEnumerable<Symbol> localResult = dict.TryGetValue(name, out var s) && sf(s)
                    ? CollectionUtils.singletonList(s)
                    : CollectionUtils.emptyList<Symbol>();

                return lookupKind == LookupKind.RECURSIVE && outer != null
                    ? localResult.Concat(outer.getSymbolsByName(name, sf))
                    : localResult;
            }
        }
    }
}
