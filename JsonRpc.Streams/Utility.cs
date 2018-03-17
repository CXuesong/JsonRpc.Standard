using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JsonRpc.Streams
{
    internal static class Utility
    {

        public static readonly UTF8Encoding UTF8NoBom = new UTF8Encoding(false, true);

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

            // IDisposable: TextReader, TextWriter, or Stream
        public static bool TryDispose(IDisposable disposable, SemaphoreSlim waitFor, object source)
        {
            // Try to wait until the operation indicated by waitFor to finish first, elegantly.
            // However, if the operation is blocking for too long, we will just abandon it,
            // which may cause disposable.Dispose to fail.
            waitFor.Wait(1000);
            try
            {
                disposable.Dispose();
                return true;
            }
            catch (InvalidOperationException ex)
            {
                // TextWriter can throw InvalidOperationException on disposal
                // when an ongoing asynchronous operation
                // has not been finished yet.
                // Theoretically, we can do nothing about it, but to wait.
                // Sadly, we can't.
                Debug.WriteLine("{0}: Cannot dispose {1}. {2}", source, disposable, ex.Message);
            }

            return false;
        }

    }
}
