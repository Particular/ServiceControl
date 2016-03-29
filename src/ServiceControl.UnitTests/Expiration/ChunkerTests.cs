
namespace ServiceControl.UnitTests.Expiration
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using ServiceControl.Infrastructure.RavenDB.Expiration;

    [TestFixture]
    public class ChunkerTests
    {
        [Test]
        public void Chunking()
        {
            List<int> starts = new List<int>();
            List<int> ends = new List<int>();

            Chunker.ExecuteInChunks(1500, (s, e) =>
            {
                starts.Add(s);
                ends.Add(e);
            });

            Assert.AreEqual(0, starts[0]);
            Assert.AreEqual(499, ends[0]);

            Assert.AreEqual(500, starts[1]);
            Assert.AreEqual(999, ends[1]);

            Assert.AreEqual(1000, starts[2]);
            Assert.AreEqual(1499, ends[2]);

            Assert.AreEqual(3, starts.Count);
            Assert.AreEqual(3, ends.Count);
        }

        [Test]
        public void LessThenChunkSize()
        {
            List<int> starts = new List<int>();
            List<int> ends = new List<int>();

            Chunker.ExecuteInChunks(1, (s, e) =>
            {
                starts.Add(s);
                ends.Add(e);
            });

            Assert.AreEqual(0, starts[0]);
            Assert.AreEqual(0, ends[0]);

            Assert.AreEqual(1, starts.Count);
            Assert.AreEqual(1, ends.Count);
        }

        [Test]
        public void OneExtra()
        {
            List<int> starts = new List<int>();
            List<int> ends = new List<int>();

            Chunker.ExecuteInChunks(1001, (s, e) =>
            {
                starts.Add(s);
                ends.Add(e);
            });

            Assert.AreEqual(0, starts[0]);
            Assert.AreEqual(499, ends[0]);

            Assert.AreEqual(500, starts[1]);
            Assert.AreEqual(999, ends[1]);

            Assert.AreEqual(1000, starts[2]);
            Assert.AreEqual(1000, ends[2]);

            Assert.AreEqual(3, starts.Count);
            Assert.AreEqual(3, ends.Count);
        }
    }
}
