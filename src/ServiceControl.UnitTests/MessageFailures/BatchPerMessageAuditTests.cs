namespace ServiceControl.UnitTests.MessageFailures
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging.Abstractions;
    using NServiceBus.Testing;
    using NUnit.Framework;
    using ServiceControl.Infrastructure.Auth;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.UnitTests.Recoverability;
    using ServiceBus.Management.Infrastructure.Settings;

    [TestFixture]
    public class BatchPerMessageAuditTests
    {
        [Test]
        public async Task RetryAllBy_ids_emits_one_message_entry_per_id_sharing_operation_id()
        {
            var audit = new RecordingMessageActionAuditLog();
            var controller = new RetryMessagesController(new Settings(), null, null, new TestableMessageSession(),
                NullLogger<RetryMessagesController>.Instance, new StubCurrentUserAccessor(new AuditUser("a", "a")), audit);

            await controller.RetryAllBy(["m-1", "m-2", "m-3"]);

            Assert.That(audit.Messages.Select(m => m.MessageId), Is.EquivalentTo(new[] { "m-1", "m-2", "m-3" }));
            var operationId = audit.Operations.Single().OperationId;
            Assert.That(audit.Messages.Select(m => m.OperationId), Is.All.EqualTo(operationId));
            Assert.That(audit.Messages, Has.All.Matches<RecordingMessageActionAuditLog.MessageEntry>(m => m.Kind == MessageActionKind.Retry));
        }
    }
}
