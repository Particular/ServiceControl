namespace ServiceControl.Persistence.Tests.RavenDB.Archiving
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.Testing;
    using NUnit.Framework;
    using ServiceControl.Infrastructure.Auth;
    using ServiceControl.MessageFailures;
    using ServiceControl.Persistence.Tests.Recoverability;
    using ServiceControl.Recoverability;

    [TestFixture]
    class ArchiveGroupPerMessageAuditTests : RavenPersistenceTestBase
    {
        readonly RecordingMessageActionAuditLog audit = new();

        public ArchiveGroupPerMessageAuditTests() =>
            RegisterServices = services =>
            {
                services.AddSingleton<ArchiveAllInGroupHandler>();
                services.AddSingleton<RetryingManager>();
                services.AddSingleton<IMessageActionAuditLog>(audit);
            };

        [Test]
        public async Task Each_archived_message_is_audited_with_the_initiating_user()
        {
            var groupId = "TestGroup";
            var user = new AuditUser("alice-sub", "Alice");
            const string operationId = "op-arch";

            using (var session = DocumentStore.OpenAsyncSession())
            {
                foreach (var id in new[] { "A", "B" })
                {
                    await session.StoreAsync(new FailedMessage
                    {
                        Id = "FailedMessages/" + id,
                        UniqueMessageId = id,
                        Status = FailedMessageStatus.Unresolved
                    });
                }

                await session.StoreAsync(new ArchiveBatch
                {
                    Id = ArchiveBatch.MakeId(groupId, ArchiveType.FailureGroup, 0),
                    DocumentIds = ["FailedMessages/A", "FailedMessages/B"]
                });

                await session.StoreAsync(new ArchiveOperation
                {
                    Id = ArchiveOperation.MakeId(groupId, ArchiveType.FailureGroup),
                    RequestId = groupId,
                    ArchiveType = ArchiveType.FailureGroup,
                    TotalNumberOfMessages = 2,
                    NumberOfMessagesArchived = 0,
                    Started = DateTime.UtcNow,
                    GroupName = "Test Group",
                    NumberOfBatches = 1,
                    CurrentBatch = 0,
                    InitiatedById = user.Id,
                    InitiatedByName = user.Name,
                    OperationId = operationId
                });

                await session.SaveChangesAsync();
            }

            var handler = ServiceProvider.GetRequiredService<ArchiveAllInGroupHandler>();
            var context = new TestableMessageHandlerContext();

            await handler.Handle(new ArchiveAllInGroup { GroupId = groupId }, context);

            Assert.That(audit.Messages.Select(m => m.MessageId), Is.EquivalentTo(new[] { "A", "B" }));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(audit.Messages, Has.All.Matches<RecordingMessageActionAuditLog.MessageEntry>(m => m.User.Equals(user)));
                Assert.That(audit.Messages, Has.All.Matches<RecordingMessageActionAuditLog.MessageEntry>(m => m.OperationId == operationId));
                Assert.That(audit.Messages, Has.All.Matches<RecordingMessageActionAuditLog.MessageEntry>(m => m.Kind == MessageActionKind.Archive));
                Assert.That(audit.Messages, Has.All.Matches<RecordingMessageActionAuditLog.MessageEntry>(m => m.Scope == MessageActionScope.Group));
            }
        }
    }
}
