using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AsyncLinq;

namespace UnitTests
{
    public static class TestExtensions
    {
        /// <summary>
        /// Converts an <see cref="IEnumerable{T}"/> into an <see cref="IAsyncEnumerable{T}"/> with the specified batch size.
        /// </summary>
        public static IAsyncEnumerable<TSource> ToAsyncEnumerable<TSource>(this IEnumerable<TSource> source, int batchSize)
        {
            return new BatchEnumerable<TSource>(Split(source, batchSize).ToList());
        }

        private static IEnumerable<IEnumerable<T>> Split<T>(IEnumerable<T> source, int size)
        {
            var list = new List<T>(size);

            foreach (var element in source)
            {
                list.Add(element);

                if (list.Count == size)
                {
                    yield return list;
                    list = new List<T>(size);
                }
            }

            if (list.Count > 0)
            {
                yield return list;
            }
        }

        private class BatchEnumerable<TSource> : IAsyncEnumerable<TSource>
        {
            private readonly IEnumerable<IEnumerable<TSource>> source;

            public BatchEnumerable(IEnumerable<IEnumerable<TSource>> source)
            {
                this.source = source;
            }

            public IAsyncEnumerator<TSource> GetEnumerator()
            {
                return new BatchEnumerator<TSource>(this.source.GetEnumerator());
            }
        }

        private class BatchEnumerator<TSource> : IAsyncEnumerator<TSource>
        {
            private readonly IEnumerator<IEnumerable<TSource>> sourceEnumerator;
            private IEnumerator<TSource> blockEnumerator;

            public BatchEnumerator(IEnumerator<IEnumerable<TSource>> sourceEnumerator)
            {
                this.sourceEnumerator = sourceEnumerator;
            }

            public void Dispose()
            {
                if (this.blockEnumerator != null)
                {
                    this.blockEnumerator.Dispose();
                    this.blockEnumerator = null;
                }

                this.sourceEnumerator.Dispose();
            }

            public Task<bool> MoveNextAsync()
            {
                if (this.blockEnumerator != null)
                {
                    this.blockEnumerator.Dispose();
                    this.blockEnumerator = null;
                }

                if (this.sourceEnumerator.MoveNext())
                {
                    this.blockEnumerator = this.sourceEnumerator.Current.GetEnumerator();
                    return Task.FromResult(true);
                }
                else
                {
                    return Task.FromResult(false);
                }
            }

            public TSource TryGetNext(out bool success)
            {
                if (this.blockEnumerator != null)
                {
                    if (this.blockEnumerator.MoveNext())
                    {
                        success = true;
                        return this.blockEnumerator.Current;
                    }
                    else
                    {
                        this.blockEnumerator.Dispose();
                        this.blockEnumerator = null;
                    }
                }

                success = false;
                return default(TSource);
            }
        }
    }
}
