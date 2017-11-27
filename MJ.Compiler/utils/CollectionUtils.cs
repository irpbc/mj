using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http.Headers;

namespace mj.compiler.utils
{
    public static class CollectionUtils
    {
        public static IList<T> emptyList<T>() => EmptyList<T>.INSTANCE;

        private class EmptyList<T> : IList<T>
        {
            internal static readonly EmptyList<T> INSTANCE = new EmptyList<T>();
            private static readonly EmptyListEnumerator<T> ENUMERATOR = new EmptyListEnumerator<T>();
            
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            public IEnumerator<T> GetEnumerator() => ENUMERATOR;
            public void Add(T item) => throw new InvalidOperationException();
            public void Clear() { }
            public bool Contains(T item) => false;
            public void CopyTo(T[] array, int arrayIndex) { }
            public bool Remove(T item) => false;
            public int Count => 0;
            public bool IsReadOnly => true;
            public int IndexOf(T item) => -1;
            public void Insert(int index, T item) => throw new InvalidOperationException();
            public void RemoveAt(int index) => throw new InvalidOperationException();

            public T this[int index] {
                get => throw new IndexOutOfRangeException();
                set => throw new IndexOutOfRangeException();
            }
        }

        private class EmptyListEnumerator<T> : IEnumerator<T>
        {
            public bool MoveNext() => false;
            public void Reset() { }
            Object IEnumerator.Current => Current;
            public T Current => throw new IndexOutOfRangeException();
            public void Dispose() { }
        }
    }
}
