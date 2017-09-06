using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AsyncLinq;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class AsyncLinqTests
    {
        static string text =
@"This is a big line
This is a bigger line
This is the biggest line
";

        [TestMethod]
        public async Task Test1()
        {
            var lines = from ln in text.GetAsyncLines()
                        from x in new[] { ln, ln }
                        where ln.Contains("a")
                        select x;

            var list = await lines.Take(3).ToListAsync();

            Assert.AreEqual(3, list.Count);
            Assert.AreEqual("This is a big line", list[0]);
            Assert.AreEqual("This is a big line", list[1]);
            Assert.AreEqual("This is a bigger line", list[2]);
        }
    }
}
