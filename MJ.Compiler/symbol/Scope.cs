using System;
using System.Collections.Generic;
using System.Linq;

using mj.compiler.utils;

using Newtonsoft.Json;

namespace mj.compiler.symbol
{
    using Filter = Func<Symbol, bool>;

    public abstract class Scope
    {
        [JsonIgnore]
        public readonly Symbol owner;

        protected Scope(Symbol owner)
        {
            this.owner = owner;
        }

        public static readonly Filter NO_FILTER = o => true;

        /// Returns Symbols with the given name. Symbols from outward Scopes are included
        /// iff lookupKind == RECURSIVE.
        public IEnumerable<Symbol> getSymbolsByName(String name, LookupKind lookupKind = LookupKind.RECURSIVE)
        {
            return getSymbolsByName(name, NO_FILTER, lookupKind);
        }

        /// Returns Symbols with the given name that match the given filter.
        /// Symbols from outward Scopes are included iff lookupKind == RECURSIVE.
        public abstract IEnumerable<Symbol> getSymbolsByName(String name, Filter sf,
                                                             LookupKind lookupKind = LookupKind.RECURSIVE);

        /// Return the first Symbol from this or outward scopes with the given name.
        /// Returns null if none.
        public Symbol findFirst(String name)
        {
            return findFirst(name, NO_FILTER);
        }

        /// Return the first Symbol from this or outward scopes with the given name that matches the
        /// given filter. Returns null if none.
        public Symbol findFirst(String name, Filter sf)
        {
            return getSymbolsByName(name, sf).FirstOrDefault();
        }

        public enum LookupKind
        {
            RECURSIVE,
            NON_RECURSIVE
        }

        public abstract class WritableScope : Scope
        {
            protected WritableScope(Symbol owner) : base(owner) { }

            /// Enter the given Symbol into this scope.
            public abstract void enter(Symbol s);

            /// Enter symbol sym in this scope if not already there.
            public abstract void enterIfAbsent(Symbol c);

            public abstract void remove(Symbol c);

            /// Construct a fresh scope within this scope, with same owner. The new scope may
            /// shares internal structures with the this scope. Used in connection with
            /// method leave if scope access is stack-like in order to avoid allocation
            /// of fresh tables.
            public WritableScope subScope() => subScope(this.owner);

            /// Construct a fresh scope within this scope, with new owner. The new scope may
            /// shares internal structures with the this scope. Used in connection with
            /// method leave if scope access is stack-like in order to avoid allocation
            /// of fresh tables.
            public abstract WritableScope subScope(Symbol newOwner);

            /// Must be called on dup-ed scopes to be able to work with the outward scope again.
            public abstract WritableScope leave();

            /// Create a new WriteableScope.
            public static WritableScope create(Symbol owner) => new ScopeImpl(owner);
        }

        private class ScopeImpl : WritableScope
        {
            [JsonIgnore]
            private WritableScope outer;

            public ScopeImpl(Symbol owner, WritableScope outer = null) : base(owner)
            {
                this.outer = outer;
            }

            private readonly MultiValueDictionary<String, Symbol> dict = new MultiValueDictionary<String, Symbol>();

            public override void enter(Symbol s) => dict.Add(s.name, s);

            public override void enterIfAbsent(Symbol c)
            {
                if (!dict.ContainsKey(c.name)) {
                    dict.Add(c.name, c);
                }
            }

            public override void remove(Symbol c) => dict.Remove(c.name);

            public override WritableScope subScope(Symbol newOwner) => new ScopeImpl(newOwner, this);
            public override WritableScope leave() => outer;

            public override IEnumerable<Symbol> getSymbolsByName(String name, Filter sf,
                                                                 LookupKind lookupKind = LookupKind.RECURSIVE)
            {
                IEnumerable<Symbol> localResult = dict.TryGetValue(name, out var s)
                    ? s.Where(sf)
                    : CollectionUtils.emptyList<Symbol>();

                return lookupKind is LookupKind.RECURSIVE && outer != null
                    ? localResult.Concat(outer.getSymbolsByName(name, sf))
                    : localResult;
            }
        }

        public class CompoundScope : WritableScope
        {
            private readonly WritableScope primary;
            private readonly Scope secondary;

            public CompoundScope(WritableScope primary, Scope secondary) : base(primary.owner)
            {
                this.primary = primary;
                this.secondary = secondary;
            }

            public override IEnumerable<Symbol> getSymbolsByName(string name, Filter sf,
                                                                 LookupKind lookupKind = LookupKind.RECURSIVE)
            {
                IEnumerable<Symbol> primaryResult = primary.getSymbolsByName(name, sf, lookupKind);
                return lookupKind == LookupKind.RECURSIVE
                    ? primaryResult.Concat(secondary.getSymbolsByName(name, sf, lookupKind))
                    : primaryResult;
            }

            public override void enter(Symbol s) => primary.enter(s);
            public override void enterIfAbsent(Symbol c) => primary.enterIfAbsent(c);
            public override void remove(Symbol c) => primary.remove(c);
            public override WritableScope subScope(Symbol newOwner) => new ScopeImpl(newOwner, this);
            public override WritableScope leave() => throw new NotSupportedException();
        }
    }
}
