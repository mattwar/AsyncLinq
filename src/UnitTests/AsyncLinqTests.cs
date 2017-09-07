using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AsyncLinq;

namespace UnitTests
{
    [TestClass]
    public class AsyncLinqTests
    {
        private static readonly int[] numbers = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        [TestMethod]
        public async Task TestToListAsync()
        {
            await TestInBatches(numbers, async (n, b) =>
            {
                var expected = n.ToList();
                var actual = await b.ToListAsync();
                AreEqual(expected, actual);
            });
        }

        private async Task TestInBatches<T>(IEnumerable<T> values, Func<IEnumerable<T>, IAsyncEnumerable<T>, Task> action)
        {
            foreach (var size in Enumerable.Range(1, values.Count()))
            {
                var batched = values.ToAsyncEnumerable(size);
                await action(values, batched);
            }
        }

        [TestMethod]
        public async Task TestSkipCount()
        {
            await TestInBatches(numbers, async (n, b) =>
            {
                var expected = n.Skip(4).ToList();
                var actual = await b.Skip(4).ToListAsync();
                AreEqual(expected, actual);
            });
        }

        [TestMethod]
        public async Task TestSkipWhile()
        {
            await TestInBatches(numbers, async (n, b) =>
            {
                var expected = n.SkipWhile(v => v < 3).ToList();
                var actual = await b.SkipWhile(v => v < 3).ToListAsync();
                AreEqual(expected, actual);
            });
        }

        [TestMethod]
        public async Task TestTakeCount()
        {
            await TestInBatches(numbers, async (n, b) =>
            {
                var expected = n.Take(4).ToList();
                var actual = await b.Take(4).ToListAsync();
                AreEqual(expected, actual);
            });
        }

        [TestMethod]
        public async Task TestTakeWhile()
        {
            await TestInBatches(numbers, async (n, b) =>
            {
                var expected = n.TakeWhile(v => v < 3).ToList();
                var actual = await b.TakeWhile(v => v < 3).ToListAsync();
                AreEqual(expected, actual);
            });
        }

        [TestMethod]
        public async Task TestCount()
        {
            await TestInBatches(numbers, async (n, b) =>
            {
                var expected = n.Count();
                var actual = await b.CountAsync();
                Assert.AreEqual(expected, actual);
            });
        }

        [TestMethod]
        public async Task TestCountPredicate()
        {
            await TestInBatches(numbers, async (n, b) =>
            {
                var expected = n.Count(v => v > 2);
                var actual = await b.CountAsync(v => v > 2);
                Assert.AreEqual(expected, actual);
            });
        }

        [TestMethod]
        public async Task TestFirst()
        {
            await TestInBatches(numbers, async (n, b) =>
            {
                var expected = n.First();
                var actual = await b.FirstAsync();
                Assert.AreEqual(expected, actual);
            });
        }

        [TestMethod]
        public async Task TestFirstOrDefault()
        {
            await TestInBatches(numbers, async (n, b) =>
            {
                var expected = n.FirstOrDefault();
                var actual = await b.FirstOrDefaultAsync();
                Assert.AreEqual(expected, actual);
            });
        }

        [TestMethod]
        public async Task TestAny()
        {
            await TestInBatches(numbers, async (n, b) =>
            {
                var expected = n.Any();
                var actual = await b.AnyAsync();
                Assert.AreEqual(expected, actual);
            });
        }

        [TestMethod]
        public async Task TestAnyPredicate()
        {
            await TestInBatches(numbers, async (n, b) =>
            {
                var expected = n.Any(v => v < 10);
                var actual = await b.AnyAsync(v => v < 10);
                Assert.AreEqual(expected, actual);
            });
        }

        [TestMethod]
        public async Task TestAll()
        {
            await TestInBatches(numbers, async (n, b) =>
            {
                var expected = n.All(v => v < 11);
                var actual = await b.AllAsync(v => v < 11);
                Assert.AreEqual(expected, actual);
            });
        }

        [TestMethod]
        public async Task TestWhere()
        {
            await TestInBatches(numbers, async (n, b) =>
            {
                var expected = n.Where(v => v % 2 == 0).ToList();
                var actual = await b.Where(v => v % 2 == 0).ToListAsync();
                AreEqual(expected, actual);
            });
        }

        [TestMethod]
        public async Task TestSelect()
        {
            await TestInBatches(numbers, async (n, b) =>
            {
                var expected = n.Select(v => v * 2).ToList();
                var actual = await b.Select(v => v * 2).ToListAsync();
                AreEqual(expected, actual);
            });
        }

        [TestMethod]
        public async Task TestSelectAsync()
        {
            await TestInBatches(numbers, async (n, b) =>
            {
                var expected = n.Select(v => v.ToString()).ToList();
                var actual = await b.Select(ToStringAsync).ToListAsync();
                AreEqual(expected, actual);
            });
        }

        private Task<string> ToStringAsync<T>(T value)
        {
            return Task.FromResult(value.ToString());
        }

        [TestMethod]
        public async Task TestSelectManyEnumerable()
        {
            await TestInBatches(numbers, async (n, b) =>
            {
                var expected = n.SelectMany(v => Enumerable.Range(1, v)).ToList();
                var actual = await b.SelectMany(v => Enumerable.Range(1, v)).ToListAsync();
                AreEqual(expected, actual);
            });
        }

        [TestMethod]
        public async Task TestSelectManyAsyncEnumerable()
        {
            await TestInBatches(numbers, async (n, b) =>
            {
                var expected = n.SelectMany(v => Enumerable.Range(1, v)).ToList();
                var actual = await b.SelectMany(v => Enumerable.Range(1, v).ToAsyncEnumerable()).ToListAsync();
                AreEqual(expected, actual);
            });
        }

        [TestMethod]
        public async Task TestSelectManyAsync()
        {
            await TestInBatches(numbers, async (n, b) =>
            {
                var expected = n.SelectMany(v => Enumerable.Range(1, v)).ToList();
                var actual = await b.SelectMany(RangeAsync).ToListAsync();
                AreEqual(expected, actual);
            });
        }

        private Task<IEnumerable<int>> RangeAsync(int n)
        {
            return Task.FromResult(Enumerable.Range(1, n));
        }

        [TestMethod]
        public async Task TestJoin()
        {
            await TestInBatches(numbers, async (n, b) =>
            {
                var expected = n.Join(n, x => x, y => y, (x, y) => x + y).ToList();
                var actual = await b.Join(b, x => x, y => y, (x, y) => x + y).ToListAsync();
                AreEqual(expected, actual);
            });
        }

        private static void AreEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual)
        {
            var expectedText = string.Join(", ", expected.Select(e => e.ToString()));
            var actualText = string.Join(", ", actual.Select(a => a.ToString()));
            Assert.AreEqual(expectedText, actualText);
        }
    }
}
