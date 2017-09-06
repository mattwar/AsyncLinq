// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncLinq
{
    /// <summary>
    /// An implementation of the LINQ pattern for <see cref="IAsyncEnumerator{T}"/>
    /// </summary>
    public static class AsyncEnumerator
    {
        /// <summary>
        /// Creates a new <see cref="IAsyncEnumerator{TSource}"/> with the ability to cancel its enumeration.
        /// </summary>
        public static IAsyncEnumerator<TSource> WithCancellation<TSource>(
            this IAsyncEnumerator<TSource> source,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.CanBeCanceled)
            {
                return new CancellableEnumerator<TSource>(source, cancellationToken);
            }
            else
            {
                return source;
            }
        }

        private class CancellableEnumerator<TSource> : IAsyncEnumerator<TSource>
        {
            private readonly IAsyncEnumerator<TSource> source;
            private readonly CancellationToken cancellationToken;

            public CancellableEnumerator(IAsyncEnumerator<TSource> source, CancellationToken cancellationToken)
            {
                this.source = source;
                this.cancellationToken = cancellationToken;
            }

            public Task<bool> MoveNextAsync()
            {
                this.cancellationToken.ThrowIfCancellationRequested();

                var task = this.source.MoveNextAsync();
                if (!task.IsCompleted)
                {
                    // this is fake cancellation at the task level so consumers get canceled,
                    // but not the actual task.
                    return task.ContinueWith(t => t.Result, this.cancellationToken);
                }
                else
                {
                    return task;
                }
            }

            public TSource TryGetNext(out bool success)
            {
                this.cancellationToken.ThrowIfCancellationRequested();
                return this.source.TryGetNext(out success);
            }

            public void Dispose()
            {
                this.source.Dispose();
            }
        }

        /// <summary>
        /// Converts the source <see cref="IAsyncEnumerator{T}"/> to a <see cref="IEnumerator{T}"/>.
        /// </summary>
        public static IAsyncEnumerator<TSource> ToAsyncEnumerator<TSource>(this IEnumerator<TSource> source)
        {
            var ae = source as IAsyncEnumerator<TSource>;
            if (ae != null)
            {
                return ae;
            }

            var wrapped = source as SyncWrapper<TSource>;
            if (wrapped != null)
            {
                return wrapped.sourceEnumerator;
            }

            return new AsyncWrapper<TSource>(source);
        }

        /// <summary>
        /// Converts the source <see cref="IAsyncEnumerator{T}"/> to a <see cref="IEnumerator{T}"/>
        /// </summary>
        public static IEnumerator<TSource> ToEnumerator<TSource>(this IAsyncEnumerator<TSource> source)
        {
            var e = source as IEnumerator<TSource>;
            if (e != null)
            {
                return e;
            }

            var wrapped = source as AsyncWrapper<TSource>;
            if (wrapped != null)
            {
                return wrapped.sourceEnumerator;
            }

            return new SyncWrapper<TSource>(source);
        }

        private class SyncWrapper<TSource> : IEnumerator<TSource>, IEnumerator, IDisposable
        {
            internal readonly IAsyncEnumerator<TSource> sourceEnumerator;
            private TSource current;

            public SyncWrapper(IAsyncEnumerator<TSource> sourceEnumerator)
            {
                this.sourceEnumerator = sourceEnumerator;
            }

            public TSource Current => this.current;

            object IEnumerator.Current => this.current;

            public void Dispose() => this.sourceEnumerator.Dispose();

            public void Reset()
            {
                throw new NotSupportedException();
            }

            public bool MoveNext()
            {
                while (true)
                {
                    bool success;
                    this.current = this.sourceEnumerator.TryGetNext(out success);
                    if (success)
                    {
                        return true;
                    }

                    if (!this.sourceEnumerator.MoveNextAsync().GetAwaiter().GetResult())
                    {
                        this.current = default(TSource);
                        return false;
                    }
                }
            }
        }

        private class AsyncWrapper<TSource> : IAsyncEnumerator<TSource>
        {
            internal readonly IEnumerator<TSource> sourceEnumerator;
            private TSource current;
            private bool hasCurrent;

            public AsyncWrapper(IEnumerator<TSource> sourceEnumerator)
            {
                this.sourceEnumerator = sourceEnumerator;
            }

            public void Dispose()
            {
                this.sourceEnumerator.Dispose();
            }

            public Task<bool> MoveNextAsync()
            {
                return Task.FromResult(this.MoveNext());
            }

            private bool MoveNext()
            {
                this.hasCurrent = this.sourceEnumerator.MoveNext();
                if (this.hasCurrent)
                {
                    this.current = this.sourceEnumerator.Current;
                    return true;
                }
                else
                {
                    this.current = default(TSource);
                    return false;
                }
            }

            public TSource TryGetNext(out bool success)
            {
                success = this.MoveNext();
                if (success)
                {
                    this.hasCurrent = false;
                    return this.current;
                }
                else
                {
                    return default(TSource);
                }
            }
        }

        /// <summary>
        /// Converts the source <see cref="IAsyncEnumerator{T}"/> to a <see cref="List{T}"/>.
        /// </summary>
        public static async Task<List<TSource>> ToListAsync<TSource>(
            this IAsyncEnumerator<TSource> source)
        {
            var list = new List<TSource>();

            while (true)
            {
                bool success;
                TSource value = source.TryGetNext(out success);
                if (success)
                {
                    list.Add(value);
                }
                else if (!(await source.MoveNextAsync().ConfigureAwait(false)))
                {
                    return list;
                }
            }
        }

        /// <summary>
        /// Returns the single element of the source sequence.
        /// </summary>
        public static async Task<TSource> SingleAsync<TSource>(this IAsyncEnumerator<TSource> source)
        {
            int count = 0;

            while (true)
            {
                bool success;
                var value = source.TryGetNext(out success);
                if (success)
                {
                    count++;

                    if (count > 1)
                    {
                        throw MoreThanOneElement();
                    }
                }

                if (!(await source.MoveNextAsync().ConfigureAwait(false)))
                {
                    if (count == 1)
                    {
                        return value;
                    }
                    else
                    {
                        throw EmptySequence();
                    }
                }
            }
        }

        /// <summary>
        /// Returns the single element of the source sequence, or default if empty.
        /// </summary>
        public static async Task<TSource> SingleOrDefaultAsync<TSource>(this IAsyncEnumerator<TSource> source)
        {
            int count = 0;

            while (true)
            {
                bool success;
                var value = source.TryGetNext(out success);

                if (success)
                {
                    count++;

                    if (count > 1)
                    {
                        throw MoreThanOneElement();
                    }
                }

                if (!(await source.MoveNextAsync().ConfigureAwait(false)))
                {
                    if (count == 1)
                    {
                        return value;
                    }
                    else
                    {
                        return default(TSource);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the first element of the source sequence.
        /// </summary>
        public static async Task<TSource> FirstAsync<TSource>(this IAsyncEnumerator<TSource> source)
        {
            while (true)
            {
                bool success;
                var value = source.TryGetNext(out success);
                if (success)
                {
                    return value;
                }

                if (!(await source.MoveNextAsync().ConfigureAwait(false)))
                {
                    throw EmptySequence();
                }
            }
        }

        /// <summary>
        /// Returns the first element of the source sequence, or default if empty.
        /// </summary>
        public static async Task<TSource> FirstOrDefaultAsync<TSource>(this IAsyncEnumerator<TSource> source)
        {
            while (true)
            {
                bool success;
                var value = source.TryGetNext(out success);
                if (success)
                {
                    return value;
                }

                if (!(await source.MoveNextAsync().ConfigureAwait(false)))
                {
                    return default(TSource);
                }
            }
        }

        /// <summary>
        /// Returns the count of elements in the source sequence.
        /// </summary>
        public static async Task<int> CountAsync<TSource>(this IAsyncEnumerator<TSource> enumerator)
        {
            int count = 0;

            while (true)
            {
                bool success;
                var value = enumerator.TryGetNext(out success);
                if (!success)
                {
                    if (!(await enumerator.MoveNextAsync().ConfigureAwait(false)))
                    {
                        return count;
                    }
                }
                else
                {
                    return count++;
                }
            }
        }

        /// <summary>
        /// Returns the count of elements in the source sequence that match the predicate.
        /// </summary>
        public static async Task<int> CountAsync<TSource>(this IAsyncEnumerator<TSource> source, Func<TSource, bool> predicate)
        {
            int count = 0;

            while (true)
            {
                bool success;
                var value = source.TryGetNext(out success);
                if (!success)
                {
                    if (!(await source.MoveNextAsync().ConfigureAwait(false)))
                    {
                        return count;
                    }
                }
                else if (predicate(value))
                {
                    return count++;
                }
            }
        }

        /// <summary>
        /// Returns true if the source sequence contains an element.
        /// </summary>
        public static async Task<bool> AnyAsync<TSource>(this IAsyncEnumerator<TSource> source)
        {
            while (true)
            {
                bool success;
                var value = source.TryGetNext(out success);
                if (!success)
                {
                    if (!(await source.MoveNextAsync().ConfigureAwait(false)))
                    {
                        return false;
                    }
                }
                else
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// Returns true if the source sequence contains an element that matches the predicate.
        /// </summary>
        public static async Task<bool> AnyAsync<TSource>(this IAsyncEnumerator<TSource> source, Func<TSource, bool> predicate)
        {
            while (true)
            {
                bool success;
                var value = source.TryGetNext(out success);
                if (!success)
                {
                    if (!(await source.MoveNextAsync().ConfigureAwait(false)))
                    {
                        return false;
                    }
                }
                else if (predicate(value))
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// Returns true if all the elements of the source sequence match the predicate.
        /// </summary>
        public static async Task<bool> AllAsync<TSource>(this IAsyncEnumerator<TSource> source, Func<TSource, bool> predicate)
        {
            while (true)
            {
                bool success;
                var value = source.TryGetNext(out success);
                if (success && !predicate(value))
                {
                    return false;
                }

                if (!(await source.MoveNextAsync().ConfigureAwait(false)))
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// Returns a sequence elements from the source sequence that match the predicate.
        /// </summary>
        public static IAsyncEnumerator<TSource> Where<TSource>(this IAsyncEnumerator<TSource> source, Func<TSource, bool> predicate)
        {
            return new WhereEnumerator<TSource>(source, predicate);
        }

        private class WhereEnumerator<TSource> : IAsyncEnumerator<TSource>
        {
            private readonly IAsyncEnumerator<TSource> sourceEnumerator;
            private readonly Func<TSource, bool> predicate;

            public WhereEnumerator(IAsyncEnumerator<TSource> sourceEnumerator, Func<TSource, bool> predicate)
            {
                this.sourceEnumerator = sourceEnumerator;
                this.predicate = predicate;
            }

            public void Dispose() => this.sourceEnumerator.Dispose();

            public Task<bool> MoveNextAsync()
            {
                return this.sourceEnumerator.MoveNextAsync();
            }

            public TSource TryGetNext(out bool success)
            {
                while (true)
                {
                    var value = this.sourceEnumerator.TryGetNext(out success);

                    if (success)
                    {
                        if (this.predicate(value))
                        {
                            return value;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    return default(TSource);
                }
            }
        }

        /// <summary>
        /// Returns a sequence of selected values, one for each element in the source sequence.
        /// </summary>
        public static IAsyncEnumerator<TCollection> Select<TSource, TCollection>(
            this IAsyncEnumerator<TSource> source,
            Func<TSource, TCollection> selector)
        {
            return new SelectEnumerator<TSource, TCollection>(source, selector);
        }

        private class SelectEnumerator<TSource, TCollection> : IAsyncEnumerator<TCollection>
        {
            private readonly IAsyncEnumerator<TSource> source;
            private readonly Func<TSource, TCollection> selector;

            public SelectEnumerator(IAsyncEnumerator<TSource> source, Func<TSource, TCollection> selector)
            {
                this.source = source;
                this.selector = selector;
            }

            public void Dispose() => this.source.Dispose();

            public Task<bool> MoveNextAsync()
            {
                return this.source.MoveNextAsync();
            }

            public TCollection TryGetNext(out bool success)
            {
                var value = this.source.TryGetNext(out success);

                if (success)
                {
                    return this.selector(value);
                }
                else
                {
                    return default(TCollection);
                }
            }
        }

        /// <summary>
        /// Returns a sequence of asynchronously selected values, one for each element in the source sequence.
        /// </summary>
        public static IAsyncEnumerator<TCollection> Select<TSource, TCollection>(
            this IAsyncEnumerator<TSource> source,
            Func<TSource, Task<TCollection>> selector)
        {
            return new SelectEnumerator2<TSource, TCollection>(source, selector);
        }

        private class SelectEnumerator2<TSource, TCollection> : IAsyncEnumerator<TCollection>
        {
            private readonly IAsyncEnumerator<TSource> sourceEnumerator;
            private readonly Func<TSource, Task<TCollection>> selector;
            private bool hasCurrent;
            private TSource current;
            private bool hasResult;
            private TCollection result;

            public SelectEnumerator2(IAsyncEnumerator<TSource> sourceEnumerator, Func<TSource, Task<TCollection>> selector)
            {
                this.sourceEnumerator = sourceEnumerator;
                this.selector = selector;
            }

            public void Dispose() => this.sourceEnumerator.Dispose();

            public Task<bool> MoveNextAsync()
            {
                if (this.hasCurrent)
                {
                    this.hasCurrent = false;
                    return this.GetResultAsync();
                }

                return this.sourceEnumerator.MoveNextAsync();
            }

            private async Task<bool> GetResultAsync()
            {
                this.result = await this.selector(this.current).ConfigureAwait(false);
                this.hasResult = true;
                return true;
            }

            public TCollection TryGetNext(out bool success)
            {
                if (this.hasResult)
                {
                    success = true;
                    this.hasResult = false;
                    return this.result;
                }

                this.current = this.sourceEnumerator.TryGetNext(out this.hasCurrent);
                success = false;
                return default(TCollection);
            }
        }

        /// <summary>
        /// Returns a sequence of selected values, zero or more for each element in the source sequence.
        /// </summary>
        public static IAsyncEnumerator<TCollection> SelectMany<TSource, TCollection>(
            this IAsyncEnumerator<TSource> source, 
            Func<TSource, IEnumerable<TCollection>> collectionSelector)
        {
            return source.SelectMany(collectionSelector, (s, c) => c);
        }

        /// <summary>
        /// Returns a sequence of selected values, zero or more for each element in the source sequence.
        /// </summary>
        public static IAsyncEnumerator<TResult> SelectMany<TSource, TCollection, TResult>(
            this IAsyncEnumerator<TSource> source,
            Func<TSource, IEnumerable<TCollection>> collectionSelector,
            Func<TSource, TCollection, TResult> resultSelector)
        {
            return new SelectManyEnumerator<TSource, TCollection, TResult>(source, collectionSelector, resultSelector);
        }

        private class SelectManyEnumerator<TSource, TCollection, TResult> : IAsyncEnumerator<TResult>
        {
            private readonly IAsyncEnumerator<TSource> sourceEnumerator;
            private readonly Func<TSource, IEnumerable<TCollection>> collectionSelector;
            private readonly Func<TSource, TCollection, TResult> resultSelector;
            private TSource sourceElement;
            private IEnumerator<TCollection> collectionEnumerator;

            public SelectManyEnumerator(
                IAsyncEnumerator<TSource> sourceEnumerator, 
                Func<TSource, IEnumerable<TCollection>> collectionSelector,
                Func<TSource, TCollection, TResult> resultSelector)
            {
                this.sourceEnumerator = sourceEnumerator;
                this.collectionSelector = collectionSelector;
                this.resultSelector = resultSelector;
            }

            public void Dispose()
            {
                if (this.collectionEnumerator != null)
                {
                    this.collectionEnumerator.Dispose();
                    this.collectionEnumerator = null;
                }

                this.sourceEnumerator.Dispose();
            }

            public Task<bool> MoveNextAsync()
            {
                return this.sourceEnumerator.MoveNextAsync();
            }

            public TResult TryGetNext(out bool success)
            {
                while (true)
                {
                    if (this.collectionEnumerator != null)
                    {
                        if (this.collectionEnumerator.MoveNext())
                        {
                            success = true;
                            return this.resultSelector(this.sourceElement, this.collectionEnumerator.Current);
                        }

                        this.collectionEnumerator.Dispose();
                        this.collectionEnumerator = null;
                    }

                    this.sourceElement = this.sourceEnumerator.TryGetNext(out success);
                    if (!success)
                    {
                        return default(TResult);
                    }

                    this.collectionEnumerator = this.collectionSelector(this.sourceElement)?.GetEnumerator();
                }
            }
        }

        /// <summary>
        /// Returns a sequence of selected values, zero or more for each element in the source sequence.
        /// </summary>
        public static IAsyncEnumerator<TCollection> SelectMany<TSource, TCollection>(
            this IAsyncEnumerator<TSource> source, 
            Func<TSource, IAsyncEnumerable<TCollection>> collectionSelector)
        {
            return source.SelectMany(collectionSelector, (s, c) => c);
        }

        /// <summary>
        /// Returns a sequence of selected values, zero or more for each element in the source sequence.
        /// </summary>
        public static IAsyncEnumerator<TResult> SelectMany<TSource, TCollection, TResult>(
            this IAsyncEnumerator<TSource> source,
            Func<TSource, IAsyncEnumerable<TCollection>> collectionSelector,
            Func<TSource, TCollection, TResult> resultSelector)
        {
            return new SelectManyEnumerator2<TSource, TCollection, TResult>(source, collectionSelector, resultSelector);
        }

        private class SelectManyEnumerator2<TSource, TCollection, TResult> : IAsyncEnumerator<TResult>
        {
            private readonly IAsyncEnumerator<TSource> sourceEnumerator;
            private readonly Func<TSource, IAsyncEnumerable<TCollection>> collectionSelector;
            private readonly Func<TSource, TCollection, TResult> resultSelector;
            private TSource current;
            private IAsyncEnumerator<TCollection> valueEnumerator;

            public SelectManyEnumerator2(
                IAsyncEnumerator<TSource> sourceEnumerator, 
                Func<TSource, IAsyncEnumerable<TCollection>> collectionSelector,
                Func<TSource, TCollection, TResult> resultSelector)
            {
                this.sourceEnumerator = sourceEnumerator;
                this.collectionSelector = collectionSelector;
                this.resultSelector = resultSelector;
            }

            public void Dispose()
            {
                if (this.valueEnumerator != null)
                {
                    this.valueEnumerator.Dispose();
                    this.valueEnumerator = null;
                }

                this.sourceEnumerator.Dispose();
            }

            public async Task<bool> MoveNextAsync()
            {
                if (this.valueEnumerator != null)
                {
                    this.valueEnumerator.Dispose();
                    this.valueEnumerator = null;
                }

                if (await this.sourceEnumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    bool success;
                    this.current = this.sourceEnumerator.TryGetNext(out success);
                    if (success)
                    {
                        this.valueEnumerator = this.collectionSelector(this.current)?.GetEnumerator();
                        return true;
                    }
                }

                this.valueEnumerator = null;
                return false;
            }

            public TResult TryGetNext(out bool success)
            {
                if (this.valueEnumerator != null)
                {
                    var collectionCurrent = this.valueEnumerator.TryGetNext(out success);
                    if (success)
                    {
                        return this.resultSelector(this.current, collectionCurrent);
                    }
                }

                success = false;
                return default(TResult);
            }
        }

        /// <summary>
        /// Returns a sequence of asynchronously selected values, zero or more for each element in the source sequence.
        /// </summary>
        public static IAsyncEnumerator<TCollection> SelectMany<TSource, TCollection>(
            this IAsyncEnumerator<TSource> source,
            Func<TSource, Task<IEnumerable<TCollection>>> collectionSelector)
        {
            return source.SelectMany(collectionSelector, (s, c) => c);
        }

        /// <summary>
        /// Returns a sequence of asynchronously selected values, zero or more for each element in the source sequence.
        /// </summary>
        public static IAsyncEnumerator<TResult> SelectMany<TSource, TCollection, TResult>(
            this IAsyncEnumerator<TSource> source,
            Func<TSource, Task<IEnumerable<TCollection>>> collectionSelector,
            Func<TSource, TCollection, TResult> resultSelector)
        {
            return new SelectManyEnumerator3<TSource, TCollection, TResult>(source, collectionSelector, resultSelector);
        }

        private class SelectManyEnumerator3<TSource, TCollection, TResult> : IAsyncEnumerator<TResult>
        {
            private readonly IAsyncEnumerator<TSource> sourceEnumerator;
            private readonly Func<TSource, Task<IEnumerable<TCollection>>> collectionSelector;
            private readonly Func<TSource, TCollection, TResult> resultSelector;
            private TSource current;
            private IEnumerator<TCollection> valueEnumerator;

            public SelectManyEnumerator3(
                IAsyncEnumerator<TSource> sourceEnumerator,
                Func<TSource, Task<IEnumerable<TCollection>>> collectionSelector,
                Func<TSource, TCollection, TResult> resultSelector)
            {
                this.sourceEnumerator = sourceEnumerator;
                this.collectionSelector = collectionSelector;
                this.resultSelector = resultSelector;
            }

            public void Dispose()
            {
                if (this.valueEnumerator != null)
                {
                    this.valueEnumerator.Dispose();
                    this.valueEnumerator = null;
                }

                this.sourceEnumerator.Dispose();
            }

            public async Task<bool> MoveNextAsync()
            {
                if (this.valueEnumerator != null)
                {
                    this.valueEnumerator.Dispose();
                    this.valueEnumerator = null;
                }

                if (await this.sourceEnumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    bool success;
                    this.current = this.sourceEnumerator.TryGetNext(out success);
                    if (success)
                    {
                        this.valueEnumerator = (await this.collectionSelector(this.current).ConfigureAwait(false)).GetEnumerator();
                        return true;
                    }
                }

                this.valueEnumerator = null;
                return false;
            }

            public TResult TryGetNext(out bool success)
            {
                if (this.valueEnumerator != null && this.valueEnumerator.MoveNext())
                {
                    success = true;
                    return this.resultSelector(this.current, this.valueEnumerator.Current);
                }
                else
                {
                    success = false;
                    return default(TResult);
                }
            }
        }

        /// <summary>
        /// Returns a sequence up to the first n elements from the source sequence.
        /// </summary>
        public static IAsyncEnumerator<TSource> Take<TSource>(
            this IAsyncEnumerator<TSource> source, 
            int count)
        {
            return new TakeEnumerator<TSource>(source, count);
        }

        private class TakeEnumerator<TSource> : IAsyncEnumerator<TSource>
        {
            private readonly IAsyncEnumerator<TSource> source;
            private int count;

            public TakeEnumerator(IAsyncEnumerator<TSource> source, int count)
            {
                this.source = source;
                this.count = count;
            }

            public Task<bool> MoveNextAsync()
            {
                if (this.count > 0)
                {
                    return this.source.MoveNextAsync();
                }
                else
                {
                    return Task.FromResult(false);
                }
            }

            public TSource TryGetNext(out bool success)
            {
                if (this.count > 0)
                {
                    var result = this.source.TryGetNext(out success);
                    if (success)
                    {
                        this.count--;
                        return result;
                    }
                }
                else
                {
                    success = false;
                }

                return default(TSource);
            }

            public void Dispose() => this.source.Dispose();
        }

        /// <summary>
        /// Returns a sequence of the elements of the source sequence up until the first element that does not match the predicate.
        /// </summary>
        public static IAsyncEnumerator<TSource> TakeWhile<TSource>(
            this IAsyncEnumerator<TSource> source, 
            Func<TSource, bool> predicate)
        {
            return new TakeWhileEnumerator<TSource>(source, predicate);
        }

        private class TakeWhileEnumerator<TSource> : IAsyncEnumerator<TSource>
        {
            private readonly IAsyncEnumerator<TSource> source;
            private readonly Func<TSource, bool> predicate;
            private bool done;

            public TakeWhileEnumerator(IAsyncEnumerator<TSource> source, Func<TSource, bool> predicate)
            {
                this.source = source;
                this.predicate = predicate;
            }

            public Task<bool> MoveNextAsync()
            {
                if (this.done)
                {
                    return Task.FromResult(false);
                }
                else
                {
                    return this.source.MoveNextAsync();
                }
            }

            public TSource TryGetNext(out bool success)
            {
                if (!this.done)
                {
                    var result = this.source.TryGetNext(out success);
                    if (success)
                    {
                        if (this.predicate(result))
                        {
                            return result;
                        }
                        else
                        {
                            this.done = true;
                        }
                    }
                }

                success = false;
                return default(TSource);
            }

            public void Dispose() => this.source.Dispose();
        }

        /// <summary>
        /// Returns the sequence of elements of the source sequence after the nth element.
        /// </summary>
        public static IAsyncEnumerator<TSource> Skip<TSource>(this IAsyncEnumerator<TSource> source, int count)
        {
            return new SkipEnumerator<TSource>(source, count);
        }

        private class SkipEnumerator<TSource> : IAsyncEnumerator<TSource>
        {
            private readonly IAsyncEnumerator<TSource> source;
            private int count;

            public SkipEnumerator(IAsyncEnumerator<TSource> source, int count)
            {
                this.source = source;
                this.count = count;
            }

            public Task<bool> MoveNextAsync()
            {
                return this.source.MoveNextAsync();
            }

            public TSource TryGetNext(out bool success)
            {
                while (this.count > 0)
                {
                    var result = this.source.TryGetNext(out success);
                    if (success)
                    {
                        this.count--;
                        continue;
                    }
                    else
                    {
                        return default(TSource);
                    }
                }

                return this.source.TryGetNext(out success);
            }

            public void Dispose() => this.source.Dispose();
        }

        /// <summary>
        /// Returns the sequence of elements from the source sequence starting with the first element that does not match the predicate.
        /// </summary>
        public static IAsyncEnumerator<TSource> SkipWhile<TSource>(
            this IAsyncEnumerator<TSource> source,
            Func<TSource, bool> predicate)
        {
            return new SkipWhileEnumerator<TSource>(source, predicate);
        }

        private class SkipWhileEnumerator<TSource> : IAsyncEnumerator<TSource>
        {
            private readonly IAsyncEnumerator<TSource> source;
            private readonly Func<TSource, bool> predicate;
            private bool done;

            public SkipWhileEnumerator(IAsyncEnumerator<TSource> source, Func<TSource, bool> predicate)
            {
                this.source = source;
                this.predicate = predicate;
            }

            public Task<bool> MoveNextAsync()
            {
                return this.source.MoveNextAsync();
            }

            public TSource TryGetNext(out bool success)
            {
                while (!this.done)
                {
                    var result = this.source.TryGetNext(out success);
                    if (success)
                    {
                        if (this.predicate(result))
                        {
                            continue;
                        }
                        else
                        {
                            this.done = true;
                            return result;
                        }
                    }
                }

                return this.source.TryGetNext(out success);
            }

            public void Dispose() => this.source.Dispose();
        }

        /// <summary>
        /// Joins two async streams into one.
        /// </summary>
        public static IAsyncEnumerator<TResult> Join<TSource1, TSource2, TJoin, TResult>(
            this IAsyncEnumerator<TSource1> source1,
            IAsyncEnumerator<TSource2> source2,
            Func<TSource1, TJoin> keySelector1,
            Func<TSource2, TJoin> keySelector2,
            Func<TSource1, TSource2, TResult> resultSelector)
            where TJoin : IComparable<TJoin>
        {
            return new JoinEnumerator<TSource1, TSource2, TJoin, TResult>(
                source1, source2, keySelector1, keySelector2, resultSelector);
        }

        private class JoinEnumerator<TSource1, TSource2, TJoin, TResult> : IAsyncEnumerator<TResult>
            where TJoin : IComparable<TJoin>
        {
            private readonly IAsyncEnumerator<TSource1> source1;
            private readonly IAsyncEnumerator<TSource2> source2;
            private readonly Func<TSource1, TJoin> keySelector1;
            private readonly Func<TSource2, TJoin> keySelector2;
            private readonly Func<TSource1, TSource2, TResult> resultSelector;
            private readonly Comparer<TJoin> comparer;
            private ILookup<TJoin, TSource2> lookup;
            private TSource1 current1;
            private IEnumerator<TSource2> currentEnumerator2;

            public JoinEnumerator(
                IAsyncEnumerator<TSource1> source1,
                IAsyncEnumerator<TSource2> source2,
                Func<TSource1, TJoin> keySelector1,
                Func<TSource2, TJoin> keySelector2,
                Func<TSource1, TSource2, TResult> resultSelector)
            {
                this.source1 = source1;
                this.source2 = source2;
                this.keySelector1 = keySelector1;
                this.keySelector2 = keySelector2;
                this.resultSelector = resultSelector;
                this.comparer = Comparer<TJoin>.Default;
            }

            public void Dispose()
            {
                this.source1.Dispose();
                this.source2.Dispose();
            }

            public Task<bool> MoveNextAsync()
            {
                if (this.lookup == null)
                {
                    return this.ComputeLookupAsync();
                }

                return this.source1.MoveNextAsync();
            }

            private async Task<bool> ComputeLookupAsync()
            {
                var list = await this.source2.ToListAsync().ConfigureAwait(false);
                this.lookup = list.ToLookup(this.keySelector2);
                return await this.source1.MoveNextAsync().ConfigureAwait(false); // pre-fetch first batch
            }

            public TResult TryGetNext(out bool success)
            {
                // if we don't have lookup table yet, fail so caller will enter MoveNextAsync
                if (this.lookup == null)
                {
                    success = false;
                    return default(TResult);
                }

                while (true)
                {
                    // if we have a match from source2 yield it.
                    if (this.currentEnumerator2 != null && this.currentEnumerator2.MoveNext())
                    {
                        success = true;
                        return resultSelector(this.current1, this.currentEnumerator2.Current);
                    }

                    // move on to next source1 element
                    this.current1 = this.source1.TryGetNext(out success);
                    if (!success)
                    {
                        // no more source1 elements available, fail so caller will enter MoveNextAsync to get more.
                        return default(TResult);
                    }

                    // lookup matching source2 elements for current source1 element
                    // and then loop back to try to produce a result
                    var elements2 = this.lookup[keySelector1(this.current1)];
                    this.currentEnumerator2 = elements2.GetEnumerator();
                }
            }
        }

        private static Exception EmptySequence()
        {
            return new InvalidOperationException("More than one element in sequence.");
        }

        private static Exception MoreThanOneElement()
        {
            return new InvalidOperationException("More than one element in sequence.");
        }
    }
}
