using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace JsonRpc.Standard
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

        private struct SemaphoreReleaser : IDisposable
        {
            private readonly SemaphoreSlim _S;

            public SemaphoreReleaser(SemaphoreSlim s)
            {
                if (s == null) throw new ArgumentNullException(nameof(s));
                _S = s;
            }

            public void Dispose()
            {
                _S.Release();
            }
        }

        public static Task<IDisposable> LockAsync(this SemaphoreSlim semaphore)
        {
            return LockAsync(semaphore, CancellationToken.None);
        }

        public static async Task<IDisposable> LockAsync(this SemaphoreSlim semaphore,
            CancellationToken cancellationToken)
        {
            if (semaphore == null) throw new ArgumentNullException(nameof(semaphore));
            await semaphore.WaitAsync(cancellationToken);
            return new SemaphoreReleaser(semaphore);
        }

        public static bool TryLock(this SemaphoreSlim semaphore, out IDisposable releaser)
        {
            if (semaphore == null) throw new ArgumentNullException(nameof(semaphore));
            if (semaphore.Wait(0))
            {
                releaser = new SemaphoreReleaser(semaphore);
                return true;
            }
            releaser = null;
            return false;
        }
    }
}
