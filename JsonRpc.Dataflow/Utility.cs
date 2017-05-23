using System;
using System.Collections.Generic;
using System.Text;

namespace JsonRpc.Dataflow
{
    internal static class Utility
    {
        public static int IndexOf<T>(this IList<T> source, IList<T> match)
        {
            return IndexOf<T>(source, match, EqualityComparer<T>.Default);
        }

        public static int IndexOf<T>(this IList<T> source, IList<T> match, IEqualityComparer<T> comparer)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (match == null) throw new ArgumentNullException(nameof(match));
            int iub = source.Count - match.Count + 1;
            // Brute force
            for (int i = 0; i < iub; i++)
            {
                for (int j = 0; j < match.Count; j++)
                {
                    if (!comparer.Equals(source[i + j], match[j])) goto NEXT;
                }
                return i;
                NEXT:
                ;
            }
            return -1;
        }

        private struct Disposable2 : IDisposable
        {
            public Disposable2(IDisposable item1, IDisposable item2)
            {
                Item1 = item1;
                Item2 = item2;
            }

            public IDisposable Item1 { get; }

            public IDisposable Item2 { get; }

            /// <inheritdoc />
            public void Dispose()
            {
                Item1.Dispose();
                Item2.Dispose();
            }
        }

        public static IDisposable CombineDisposable(IDisposable item1, IDisposable item2)
        {
            if (item1 == null) throw new ArgumentNullException(nameof(item1));
            if (item2 == null) throw new ArgumentNullException(nameof(item2));
            return new Disposable2(item1, item2);
        }
    }
}
