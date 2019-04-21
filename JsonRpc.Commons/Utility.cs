using System;
using System.Threading.Tasks;

namespace JsonRpc
{
    internal static class Utility
    {

        public static Type GetTaskResultType(Type taskType)
        {
            if (taskType == null) throw new ArgumentNullException(nameof(taskType));
            if (taskType == typeof(Task)) return typeof(void);
            if (taskType.IsConstructedGenericType && taskType.GetGenericTypeDefinition() == typeof(Task<>))
                return taskType.GenericTypeArguments[0];
            return null;
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
