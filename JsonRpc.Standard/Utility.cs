using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

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

        public static bool ValidateRequestId(object id)
        {
            return id == null || id is string
                   || id is byte || id is short || id is int || id is long
                   || id is sbyte || id is ushort || id is uint || id is ulong;
        }

        public static Type GetTaskResultType(Type taskType)
        {
            if (taskType == null) throw new ArgumentNullException(nameof(taskType));
            if (taskType == typeof(Task)) return typeof(void);
            if (taskType.IsConstructedGenericType && taskType.GetGenericTypeDefinition() == typeof(Task<>))
                return taskType.GenericTypeArguments[0];
            return null;
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

        public static Task<T> ReceiveAsync<T>(this ISourceBlock<T> source, Predicate<T> predicate,
            CancellationToken cancellationToken)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<T>(cancellationToken);
            var rec = source as IReceivableSourceBlock<T>;
            if (rec != null)
            {
                if (rec.TryReceive(predicate, out var item)) return Task.FromResult(item);
            }
            return ((Func<ISourceBlock<T>, Predicate<T>, CancellationToken, Task<T>>) (async (s, p, c) =>
            {
                var target = new WriteOnceBlock<T>(null, new DataflowBlockOptions {CancellationToken = c});
                using (s.LinkTo(target, p))
                {
                    return await target.ReceiveAsync(c).ConfigureAwait(false);
                }
            }))(source, predicate, cancellationToken);
        }

    }
}
