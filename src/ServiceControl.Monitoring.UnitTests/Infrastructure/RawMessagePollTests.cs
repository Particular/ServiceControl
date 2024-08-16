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

            Assert.That(message.Length, Is.EqualTo(1));
            Assert.That(message.Entries[0].DateTicks, Is.EqualTo(3));
            Assert.That(message.Entries[0].Value, Is.EqualTo(4));

            pool.Release(message);
            Assert.That(message.Length, Is.EqualTo(0));
            Assert.That(message.Entries[0].DateTicks, Is.EqualTo(0));
            Assert.That(message.Entries[0].Value, Is.EqualTo(0));
        }
    }
}