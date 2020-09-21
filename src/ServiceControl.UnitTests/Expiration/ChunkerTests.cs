namespace ServiceControl.UnitTests.Expiration
{
    using System.Collections.Generic;
    using System.Threading;
    using NUnit.Framework;
    using ServiceControl.Infrastructure.RavenDB;

    [TestFixture]
    public class ChunkerTests
    {
        [Test]
        public void Chunking()
        {
            var starts = new List<int>();
            var ends = new List<int>();

            var count = Chunker.ExecuteInChunks(1500, (startList, endList, s, e) =>
            {
                startList.Add(s);
                endList.Add(e);
                return 1;
            }, starts, ends, CancellationToken.None);

            Assert.AreEqual(0, starts[0]);
            Assert.AreEqual(499, ends[0]);

            Assert.AreEqual(500, starts[1]);
            Assert.AreEqual(999, ends[1]);

            Assert.AreEqual(1000, starts[2]);
            Assert.AreEqual(1499, ends[2]);

            Assert.AreEqual(3, starts.Count);
            Assert.AreEqual(3, ends.Count);
            Assert.AreEqual(3, count);
        }

        [Test]
        public void LessThenChunkSize()
        {
            var starts = new List<int>();
            var ends = new List<int>();

            var count = Chunker.ExecuteInChunks(1, (startList, endList, s, e) =>
            {
                startList.Add(s);
                endList.Add(e);
                return 1;
            }, starts, ends, CancellationToken.None);

            Assert.AreEqual(0, starts[0]);
            Assert.AreEqual(0, ends[0]);

            Assert.AreEqual(1, starts.Count);
            Assert.AreEqual(1, ends.Count);
            Assert.AreEqual(1, count);
        }

        [Test]
        public void OneExtra()
        {
            var starts = new List<int>();
            var ends = new List<int>();

            var count = Chunker.ExecuteInChunks(1001, (startList, endList, s, e) =>
            {
                startList.Add(s);
                endList.Add(e);
                return 1;
            }, starts, ends, CancellationToken.None);

            Assert.AreEqual(0, starts[0]);
            Assert.AreEqual(499, ends[0]);

            Assert.AreEqual(500, starts[1]);
            Assert.AreEqual(999, ends[1]);

            Assert.AreEqual(1000, starts[2]);
            Assert.AreEqual(1000, ends[2]);

            Assert.AreEqual(3, starts.Count);
            Assert.AreEqual(3, ends.Count);
            Assert.AreEqual(3, count);
        }
    }
}