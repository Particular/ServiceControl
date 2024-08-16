namespace ServiceControl.Persistence.Tests.RavenDB.Expiration
{
    using System.Collections.Generic;
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
            }, starts, ends);

            Assert.Multiple(() =>
            {
                Assert.That(starts[0], Is.EqualTo(0));
                Assert.That(ends[0], Is.EqualTo(499));

                Assert.That(starts[1], Is.EqualTo(500));
                Assert.That(ends[1], Is.EqualTo(999));

                Assert.That(starts[2], Is.EqualTo(1000));
                Assert.That(ends[2], Is.EqualTo(1499));

                Assert.That(starts.Count, Is.EqualTo(3));
                Assert.That(ends.Count, Is.EqualTo(3));
                Assert.That(count, Is.EqualTo(3));
            });
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
            }, starts, ends);

            Assert.Multiple(() =>
            {
                Assert.That(starts[0], Is.EqualTo(0));
                Assert.That(ends[0], Is.EqualTo(0));

                Assert.That(starts.Count, Is.EqualTo(1));
                Assert.That(ends.Count, Is.EqualTo(1));
                Assert.That(count, Is.EqualTo(1));
            });
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
            }, starts, ends);

            Assert.Multiple(() =>
            {
                Assert.That(starts[0], Is.EqualTo(0));
                Assert.That(ends[0], Is.EqualTo(499));

                Assert.That(starts[1], Is.EqualTo(500));
                Assert.That(ends[1], Is.EqualTo(999));

                Assert.That(starts[2], Is.EqualTo(1000));
                Assert.That(ends[2], Is.EqualTo(1000));

                Assert.That(starts.Count, Is.EqualTo(3));
                Assert.That(ends.Count, Is.EqualTo(3));
                Assert.That(count, Is.EqualTo(3));
            });
        }
    }
}