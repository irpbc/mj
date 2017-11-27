using System.Collections.Generic;
using System;

namespace mj.compiler.utils
{
    public class Context
    {
        public abstract class Key { }

        public sealed class Key<T> : Key where T : class { }

        public delegate T Factory<out T>(Context c);

        protected readonly IDictionary<Key, Object> dict = new Dictionary<Key, Object>();

        public void put<T>(Key<T> key, Factory<T> fac) where T : class
        {
            if (dict.ContainsKey(key)) {
                throw new InvalidOperationException("duplicate context value");
            }
            dict.Add(key, fac);
        }

        public void put<T>(Key<T> key, T data) where T : class
        {
            if (isOfGenericType<T>(typeof(Factory<>))) {
                throw new InvalidOperationException("T extends Context.Factory");
            }
            if (dict.ContainsKey(key)) {
                throw new InvalidOperationException("duplicate context value");
            }
            dict.Add(key, data);
        }

        public bool tryGet<T>(Key<T> key, out T val) where T : class
        {
            if (isOfGenericType<T>(typeof(Factory<>))) {
                throw new InvalidOperationException("T extends Context.Factory");
            }
            if (dict.TryGetValue(key, out var o)) {
                if (o is Factory<T> fac) {
                    o = fac(this);
                    // TODO: What is this: Assert.check(ht.get(key) == o);
                }
                val = (T)o;
                return true;
            }
            val = null;
            return false;
        }

        private static bool isOfGenericType<T>(Type genericTypeDef)
        {
            return typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == genericTypeDef;
        }

        /*
         * The key table, providing a unique Key<T> for each Class<T>.
         */
        //private readonly IDictionary<Type, Key> keyDict = new Dictionary<Type, Key>();

        /*protected Key<T> key<T>() where T : class
        {
            Key<T> k = (Key<T>)keyDict[typeof(T)];
            if (k == null) {
                k = new Key<T>();
                keyDict.Add(typeof(T), k);
            }
            return k;
        }*/

        //public T get<T>() where T : class => get(key<T>());
        //public void put<T>(T data) where T : class => put(key<T>(), data);
        //public void put<T>(Factory<T> fac) where T : class => put(key<T>(), fac);

        public void dump()
        {
            foreach (Object value in dict.Values) {
                Console.Error.WriteLine(value?.GetType());
            }
        }
    }
}
