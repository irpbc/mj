using mj.compiler.utils;

namespace mj.compiler.symbol
{
    public class Typings
    {
        private static readonly Context.Key<Typings> CONTEXT_KEY = new Context.Key<Typings>();

        public static Typings instance(Context ctx) =>
            ctx.tryGet(CONTEXT_KEY, out var instance) ? instance : new Typings(ctx);

        private readonly Symtab symtab;

        private Typings(Context ctx)
        {
            ctx.put(CONTEXT_KEY, this);

            symtab = Symtab.instance(ctx);
        }

        public bool isAssignableFrom(Type left, Type right)
        {
            // Stops propagation of errors to 
            // eliminate useless error messages.
            if (left.IsError || right.IsError) {
                return true;
            }
            if (left.IsNumeric && right.IsNumeric) {
                return left.Tag.isNumericAssignableFrom(right.Tag);
            }

            if (right == symtab.bottomType && left.IsRefType) {
                return true;
            }
            
            if (left.IsArray || right.IsArray) {
                return left == right;
            }
            
            // compare base types to account for constants
            return left.BaseType == right.BaseType;
        }
    }
}
