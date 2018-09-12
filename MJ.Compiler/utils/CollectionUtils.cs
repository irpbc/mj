using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http.Headers;

namespace mj.compiler.utils
{
    public static class CollectionUtils
    {
        public static IList<T> singletonList<T>(T elem) => new SingletonList<T>(elem);

        private class SingletonList<T> : IList<T>
        {
            private readonly T elem;
            public SingletonList(T elem) => this.elem = elem;

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            public IEnumerator<T> GetEnumerator() => new Enumerator(this);
            public void Add(T item) => throw new InvalidOperationException();
            public void Clear() => throw new InvalidOperationException();
            public bool Contains(T item) => elem.Equals(item);
            public void CopyTo(T[] array, int arrayIndex) => array[arrayIndex] = elem;
            public bool Remove(T item) => throw new InvalidOperationException();
            public int Count => 1;
            public bool IsReadOnly => true;
            public int IndexOf(T item) => elem.Equals(item) ? 0 : -1;
            public void Insert(int index, T item) => throw new InvalidOperationException();
            public void RemoveAt(int index) => throw new InvalidOperationException();

            public T this[int index] {
                get => index == 0 ? elem : throw new IndexOutOfRangeException();
                set => throw new InvalidOperationException();
            }

            private class Enumerator : IEnumerator<T>
            {
                private readonly SingletonList<T> list;
                private bool moved = false;
                public Enumerator(SingletonList<T> list) => this.list = list;

                public bool MoveNext()
                {
                    if (moved) {
                        return false;
                    }
                    moved = true;
                    return true;
                }

                public void Reset() => moved = false;
                public T Current => list.elem;
                Object IEnumerator.Current => Current;
                public void Dispose() { }
            }
        }

        public static IList<T> emptyList<T>() => EmptyList<T>.INSTANCE;

        internal class EmptyList<T> : IList<T>
        {
            internal static readonly EmptyList<T> INSTANCE = new EmptyList<T>();
            private static readonly Enumerator ENUMERATOR = new Enumerator();

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

            private class Enumerator : IEnumerator<T>
            {
                public bool MoveNext() => false;
                public void Reset() { }
                Object IEnumerator.Current => Current;
                public T Current => throw new IndexOutOfRangeException();
                public void Dispose() { }
            }
        }

        public static IList<O> convert<I, O>(this IList<I> input, Func<I, O> func)
        {
            if (input.Count == 0) {
                return emptyList<O>();
            }
            if (input.Count == 1) {
                return singletonList(func(input[0]));
            }
            O[] output = new O[input.Count];
            for (var i = 0; i < input.Count; i++) {
                output[i] = func(input[i]);
            }
            return output;
        }
    }
}
