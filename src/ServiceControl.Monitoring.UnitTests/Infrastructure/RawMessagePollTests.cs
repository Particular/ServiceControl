namespace ServiceControl.Monitoring.UnitTests.Infrastructure
{
    using Messaging;
    using NUnit.Framework;

    public class RawMessagePollTests
    {
        [Test]
        public void Message_lifecycle_is_preserved()
        {
            var pool = new RawMessage.Pool<TaggedLongValueOccurrence>();
            var message = pool.Lease();

            message.TryRecord(3, 4);

            Assert.AreEqual(1, message.Length);
            Assert.AreEqual(3, message.Entries[0].DateTicks);
            Assert.AreEqual(4, message.Entries[0].Value);
            
            pool.Release(message);
            Assert.AreEqual(0, message.Length);
            Assert.AreEqual(0, message.Entries[0].DateTicks);
            Assert.AreEqual(0, message.Entries[0].Value);
        }
    }
}