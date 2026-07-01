namespace ServiceControl.UnitTests.MessageFailures
{
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus.Testing;
    using NUnit.Framework;
    using ServiceControl.Infrastructure.Auth;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.UnitTests.Recoverability;

    [TestFixture]
    public class ArchiveUnarchivePendingAuditTests
    {
        static readonly AuditUser User = new("alice-sub", "Alice");
        static StubCurrentUserAccessor Accessor => new(User);

        [Test]
        public async Task ArchiveBatch_emits_batch_archive_operation()
        {
            var audit = new RecordingMessageActionAuditLog();
            var controller = new ArchiveMessagesController(new TestableMessageSession(), null, Accessor, audit);

            await controller.ArchiveBatch(new[] { "m-1", "m-2", "m-3" });

            var op = audit.Operations.Single();
            Assert.That(op.Kind, Is.EqualTo(MessageActionKind.Archive));
            Assert.That(op.Scope, Is.EqualTo(MessageActionScope.Batch));
            Assert.That(op.Count, Is.EqualTo(3));
        }

        [Test]
        public async Task Archive_single_emits_single_archive_operation()
        {
            var audit = new RecordingMessageActionAuditLog();
            var controller = new ArchiveMessagesController(new TestableMessageSession(), null, Accessor, audit);

            await controller.Archive("m-1");

            var op = audit.Operations.Single();
            Assert.That(op.Kind, Is.EqualTo(MessageActionKind.Archive));
            Assert.That(op.Scope, Is.EqualTo(MessageActionScope.Single));
            Assert.That(op.Resource, Is.EqualTo("m-1"));
            Assert.That(op.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task Unarchive_ids_emits_batch_unarchive_operation()
        {
            var audit = new RecordingMessageActionAuditLog();
            var controller = new UnArchiveMessagesController(new TestableMessageSession(), Accessor, audit);

            await controller.Unarchive(new[] { "m-1", "m-2" });

            var op = audit.Operations.Single();
            Assert.That(op.Kind, Is.EqualTo(MessageActionKind.Unarchive));
            Assert.That(op.Scope, Is.EqualTo(MessageActionScope.Batch));
        }

        [Test]
        public async Task Unarchive_range_emits_range_unarchive_operation()
        {
            var audit = new RecordingMessageActionAuditLog();
            var controller = new UnArchiveMessagesController(new TestableMessageSession(), Accessor, audit);

            await controller.Unarchive("2024-01-01T00:00:00Z", "2024-01-02T00:00:00Z");

            var op = audit.Operations.Single();
            Assert.That(op.Kind, Is.EqualTo(MessageActionKind.Unarchive));
            Assert.That(op.Scope, Is.EqualTo(MessageActionScope.Range));
            Assert.That(op.Resource, Is.EqualTo("2024-01-01T00:00:00Z...2024-01-02T00:00:00Z"));
            Assert.That(op.Count, Is.Null);
        }

        [Test]
        public async Task RetryBy_ids_emits_batch_retry_operation()
        {
            var audit = new RecordingMessageActionAuditLog();
            var controller = new PendingRetryMessagesController(new TestableMessageSession(), Accessor, audit);

            await controller.RetryBy(new[] { "m-1", "m-2" });

            var op = audit.Operations.Single();
            Assert.That(op.Kind, Is.EqualTo(MessageActionKind.Retry));
            Assert.That(op.Scope, Is.EqualTo(MessageActionScope.Batch));
            Assert.That(op.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task RetryBy_request_emits_queue_retry_operation()
        {
            var audit = new RecordingMessageActionAuditLog();
            var controller = new PendingRetryMessagesController(new TestableMessageSession(), Accessor, audit);

            await controller.RetryBy(new PendingRetryMessagesController.PendingRetryRequest { QueueAddress = "queue-a" });

            var op = audit.Operations.Single();
            Assert.That(op.Kind, Is.EqualTo(MessageActionKind.Retry));
            Assert.That(op.Scope, Is.EqualTo(MessageActionScope.Queue));
            Assert.That(op.Resource, Is.EqualTo("queue-a"));
            Assert.That(op.Count, Is.Null);
        }
    }
}
