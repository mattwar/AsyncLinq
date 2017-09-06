using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncLinq
{
    /// <summary>
    /// An implementation of the LINQ pattern for <see cref="IAsyncEnumerable{T}"/>
    /// </summary>
    public static class AsyncEnumerable
    {
        /// <summary>
        /// Creates a new <see cref="IAsyncEnumerable{TSource}"/> with the ability to cancel its enumeration.
        /// </summary>
        public static IAsyncEnumerable<TSource> WithCancellation<TSource>(
            this IAsyncEnumerable<TSource> source,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.CanBeCanceled)
            {
                return new CancellableEnumerable<TSource>(source, cancellationToken);
            }
            else
            {
                return source;
            }
        }

        private class CancellableEnumerable<TSource> : IAsyncEnumerable<TSource>
        {
            private readonly IAsyncEnumerable<TSource> source;
            private readonly CancellationToken cancellationToken;

            public CancellableEnumerable(IAsyncEnumerable<TSource> source, CancellationToken cancellationToken)
            {
                this.source = source;
                this.cancellationToken = cancellationToken;
            }

            public IAsyncEnumerator<TSource> GetEnumerator()
            {
                this.cancellationToken.ThrowIfCancellationRequested();
                return this.source.GetEnumerator().WithCancellation(this.cancellationToken);
            }
        }

        /// <summary>
        /// Converts the source <see cref="IEnumerable{T}"/> to <see cref="IAsyncEnumerable{T}"/>
        /// </summary>
        public static IAsyncEnumerable<TSource> ToAsyncEnumerable<TSource>(this IEnumerable<TSource> source)
        {
            var ae = source as IAsyncEnumerable<TSource>;
            if (ae != null)
            {
                return ae;
            }

            var wrapped = source as SyncWrapper<TSource>;
            if (wrapped != null)
            {
                return wrapped.source;
            }

            return new AsyncWrapper<TSource>(source);
        }

        /// <summary>
        /// Converts the source <see cref="IAsyncEnumerable{T}"/> to <see cref="IEnumerable{T}"/>
        /// </summary>
        public static IEnumerable<TSource> ToEnumerable<TSource>(this IAsyncEnumerable<TSource> source)
        {
            var e = source as IEnumerable<TSource>;
            if (e != null)
            {
                return e;
            }

            var wrapped = source as AsyncWrapper<TSource>;
            if (wrapped != null)
            {
                return wrapped.source;
            }

            return new SyncWrapper<TSource>(source);
        }

        private class AsyncWrapper<TSource> : IAsyncEnumerable<TSource>
        {
            internal readonly IEnumerable<TSource> source;

            public AsyncWrapper(IEnumerable<TSource> source)
            {
                this.source = source;
            }

            public IAsyncEnumerator<TSource> GetEnumerator()
            {
                return this.source.GetEnumerator().ToAsyncEnumerator();
            }
        }

        private class SyncWrapper<TSource> : IEnumerable<TSource>, IEnumerable
        {
            internal readonly IAsyncEnumerable<TSource> source;

            public SyncWrapper(IAsyncEnumerable<TSource> source)
            {
                this.source = source;
            }

            public IEnumerator<TSource> GetEnumerator()
            {
                return this.source.GetEnumerator().ToEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }

        /// <summary>
        /// Convert the source <see cref="IAsyncEnumerable{T}"/> to a <see cref="List{T}"/>
        /// </summary>
        public static async Task<List<TSource>> ToListAsync<TSource>(
            this IAsyncEnumerable<TSource> source)
        {
            using (var enumerator = source.GetEnumerator())
            {
                return await enumerator.ToListAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Returns the single element of the source sequence.
        /// </summary>
        public static Task<TSource> SingleAsync<TSource>(this IAsyncEnumerable<TSource> source)
        {
            using (var enumerator = source.GetEnumerator())
            {
                return enumerator.SingleAsync();
            }
        }

        /// <summary>
        /// Returns the single element of the source sequence, or default if empty./>
        /// </summary>
        public static Task<TSource> SingleOrDefaultAsync<TSource>(this IAsyncEnumerable<TSource> source)
        {
            using (var enumerator = source.GetEnumerator())
            {
                return enumerator.SingleOrDefaultAsync();
            }
        }

        /// <summary>
        /// Returns the first element of the source sequence.
        /// </summary>
        public static Task<TSource> FirstAsync<TSource>(this IAsyncEnumerable<TSource> source)
        {
            using (var enumerator = source.GetEnumerator())
            {
                return enumerator.FirstAsync();
            }
        }

        /// <summary>
        /// Returns the first element of the source sequence, or default if empty.
        /// </summary>
        public static Task<TSource> FirstOrDefaultAsync<TSource>(this IAsyncEnumerable<TSource> source)
        {
            using (var enumerator = source.GetEnumerator())
            {
                return enumerator.FirstOrDefaultAsync();
            }
        }

        /// <summary>
        /// Returns the number of elements in the source sequence.
        /// </summary>
        public static Task<int> CountAsync<TSource>(this IAsyncEnumerable<TSource> source)
        {
            using (var enumerator = source.GetEnumerator())
            {
                return enumerator.CountAsync();
            }
        }

        /// <summary>
        /// Returns the number of elements in the source sequence that match the predicate.
        /// </summary>
        public static Task<int> CountAsync<TSource>(this IAsyncEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator())
            {
                return enumerator.CountAsync(predicate);
            }
        }

        /// <summary>
        /// Returns true if any element exists in the source sequence.
        /// </summary>
        public static Task<bool> AnyAsync<TSource>(this IAsyncEnumerable<TSource> source)
        {
            using (var enumerator = source.GetEnumerator())
            {
                return enumerator.AnyAsync();
            }
        }

        /// <summary>
        /// Returns true if any element in the source sequence matches the predicate.
        /// </summary>
        public static Task<bool> AnyAsync<TSource>(this IAsyncEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator())
            {
                return enumerator.AnyAsync(predicate);
            }
        }

        /// <summary>
        /// Returns true if all elements in the source sequence match the predicate.
        /// </summary>
        public static Task<bool> AllAsync<TSource>(this IAsyncEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator())
            {
                return enumerator.AllAsync(predicate);
            }
        }

        /// <summary>
        /// Returns a sequence elements from the source sequence that match the predicate.
        /// </summary>
        public static IAsyncEnumerable<TSource> Where<TSource>(this IAsyncEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            return new Enumerable<TSource>(() => source.GetEnumerator().Where(predicate));
        }

        /// <summary>
        /// Returns a sequence of selected values, one for each element in the source sequence.
        /// </summary>
        public static IAsyncEnumerable<TResult> Select<TSource, TResult>(
            this IAsyncEnumerable<TSource> source,
            Func<TSource, TResult> selector)
        {
            return new Enumerable<TResult>(() => source.GetEnumerator().Select(selector));
        }

        /// <summary>
        /// Returns a sequence of asynchronously selected values, one for each element in the source sequence.
        /// </summary>
        public static IAsyncEnumerable<TResult> Select<TSource, TResult>(
            this IAsyncEnumerable<TSource> source,
            Func<TSource, Task<TResult>> selector)
        {
            return new Enumerable<TResult>(() => source.GetEnumerator().Select(selector));
        }

        /// <summary>
        /// Returns a sequence of selected values, zero or more for each element in the source sequence.
        /// </summary>
        public static IAsyncEnumerable<TCollection> SelectMany<TSource, TCollection>(
            this IAsyncEnumerable<TSource> source,
            Func<TSource, IEnumerable<TCollection>> collectionSelector)
        {
            return source.SelectMany(collectionSelector, (s, c) => c);
        }

        /// <summary>
        /// Returns a sequence of selected values, zero or more for each element in the source sequence.
        /// </summary>
        public static IAsyncEnumerable<TResult> SelectMany<TSource, TCollection, TResult>(
            this IAsyncEnumerable<TSource> source,
            Func<TSource, IEnumerable<TCollection>> collectionSelector,
            Func<TSource, TCollection, TResult> resultSelector)
        {
            return new Enumerable<TResult>(() => source.GetEnumerator().SelectMany(collectionSelector, resultSelector));
        }

        /// <summary>
        /// Returns a sequence of selected values, zero or more for each element in the source sequence.
        /// </summary>
        public static IAsyncEnumerable<TCollection> SelectMany<TSource, TCollection>(
            this IAsyncEnumerable<TSource> source,
            Func<TSource, IAsyncEnumerable<TCollection>> collectionSelector)
        {
            return source.SelectMany(collectionSelector, (s, c) => c);
        }

        /// <summary>
        /// Returns a sequence of selected values, zero or more for each element in the source sequence.
        /// </summary>
        public static IAsyncEnumerable<TResult> SelectMany<TSource, TCollection, TResult>(
            this IAsyncEnumerable<TSource> source,
            Func<TSource, IAsyncEnumerable<TCollection>> collectionSelector,
            Func<TSource, TCollection, TResult> resultSelector)
        {
            return new Enumerable<TResult>(() => source.GetEnumerator().SelectMany(collectionSelector, resultSelector));
        }

        /// <summary>
        /// Returns a sequence of asynchronously selected values, zero or more for each element in the source sequence.
        /// </summary>
        public static IAsyncEnumerable<TCollection> SelectMany<TSource, TCollection>(
            this IAsyncEnumerable<TSource> source,
            Func<TSource, Task<IEnumerable<TCollection>>> collectionSelector)
        {
            return source.SelectMany(collectionSelector, (s, c) => c);
        }

        /// <summary>
        /// Returns a sequence of asynchronously selected values, zero or more for each element in the source sequence.
        /// </summary>
        public static IAsyncEnumerable<TResult> SelectMany<TSource, TCollection, TResult>(
            this IAsyncEnumerable<TSource> source,
            Func<TSource, Task<IEnumerable<TCollection>>> collectionSelector,
            Func<TSource, TCollection, TResult> resultSelector)
        {
            return new Enumerable<TResult>(() => source.GetEnumerator().SelectMany(collectionSelector, resultSelector));
        }

        /// <summary>
        /// Returns a sequence up to the first n elements from the source sequence.
        /// </summary>
        public static IAsyncEnumerable<TSource> Take<TSource>(this IAsyncEnumerable<TSource> source, int count)
        {
            return new Enumerable<TSource>(() => source.GetEnumerator().Take(count));
        }

        /// <summary>
        /// Returns a sequence of the elements of the source sequence up until the first element that does not match the predicate.
        /// </summary>
        public static IAsyncEnumerable<TSource> TakeWhile<TSource>(
            this IAsyncEnumerable<TSource> source,
            Func<TSource, bool> predicate)
        {
            return new Enumerable<TSource>(() => source.GetEnumerator().TakeWhile(predicate));
        }

        /// <summary>
        /// Returns the sequence of elements of the source sequence after the nth element.
        /// </summary>
        public static IAsyncEnumerable<TSource> Skip<TSource>(this IAsyncEnumerable<TSource> source, int count)
        {
            return new Enumerable<TSource>(() => source.GetEnumerator().Skip(count));
        }

        /// <summary>
        /// Returns the sequence of elements from the source sequence starting with the first element that does not match the predicate.
        /// </summary>
        public static IAsyncEnumerable<TSource> SkipWhile<TSource>(
            this IAsyncEnumerable<TSource> source,
            Func<TSource, bool> predicate)
        {
            return new Enumerable<TSource>(() => source.GetEnumerator().SkipWhile(predicate));
        }

        /// <summary>
        /// Joins two async streams into one.
        /// </summary>
        public static IAsyncEnumerable<TResult> Join<TSource1, TSource2, TJoin, TResult>(
            this IAsyncEnumerable<TSource1> source1,
            IAsyncEnumerable<TSource2> source2,
            Func<TSource1, TJoin> keySelector1,
            Func<TSource2, TJoin> keySelector2,
            Func<TSource1, TSource2, TResult> resultSelector)
            where TJoin : IComparable<TJoin>
        {
            return new Enumerable<TResult>(() => source1.GetEnumerator().Join(
                    source2.GetEnumerator(),
                    keySelector1,
                    keySelector2,
                    resultSelector));
        }

        private class Enumerable<TResult> : IAsyncEnumerable<TResult>
        {
            private readonly Func<IAsyncEnumerator<TResult>> fnGetEnumerator;

            public Enumerable(Func<IAsyncEnumerator<TResult>> fnGetEnumerator)
            {
                this.fnGetEnumerator = fnGetEnumerator;
            }
            public IAsyncEnumerator<TResult> GetEnumerator() => fnGetEnumerator();
        }
    }
}
