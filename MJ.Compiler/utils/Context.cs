using System.Collections.Generic;
using System;

namespace mj.compiler.utils
{
    public class Context
    {
        public abstract class Key { }

        public sealed class Key<T> : Key where T : class { }

        protected readonly IDictionary<Key, Object> dict = new Dictionary<Key, Object>();

        public void put<T>(Key<T> key, T data) where T : class
        {
            if (dict.ContainsKey(key)) {
                throw new InvalidOperationException("duplicate context value");
            }
            dict.Add(key, data);
        }

        public bool tryGet<T>(Key<T> key, out T val) where T : class
        {
            if (dict.TryGetValue(key, out var o)) {
                val = (T)o;
                return true;
            }
            val = null;
            return false;
        }

        public void dump()
        {
            foreach (Object value in dict.Values) {
                Console.Error.WriteLine(value?.GetType());
            }
        }
    }
}
