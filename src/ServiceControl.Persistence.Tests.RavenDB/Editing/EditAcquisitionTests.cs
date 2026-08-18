namespace ServiceControl.Persistence.Tests.RavenDB.Editing;

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Raven.Client;
using ServiceControl.Contracts.Operations;
using ServiceControl.MessageFailures;

class EditAcquisitionTests : PersistenceTestBase
{
    [Test]
    public async Task TryBeginEdit_applies_the_failed_message_expiration()
    {
        var failedMessageId = Guid.NewGuid().ToString();
        var attemptedAt = DateTime.UtcNow;
        var failedMessage = await SeedFailedMessage(new FailedMessage
        {
            UniqueMessageId = failedMessageId,
            Status = FailedMessageStatus.Unresolved,
            ProcessingAttempts =
            [
                new FailedMessage.ProcessingAttempt
                {
                    AttemptedAt = attemptedAt,
                    MessageId = Guid.NewGuid().ToString(),
                    Body = "body",
                    Headers = [],
                    FailureDetails = new FailureDetails
                    {
                        AddressOfFailingEndpoint = "Shipping",
                        TimeOfFailure = attemptedAt
                    }
                }
            ]
        });

        var result = await EditFailedMessagesStore.TryBeginEdit(failedMessageId, Guid.NewGuid().ToString());

        using var session = PersistenceTestsContext.DocumentStore.OpenAsyncSession();
        var persisted = await session.LoadAsync<FailedMessage>(failedMessage.Id);
        var metadata = session.Advanced.GetMetadataFor(persisted);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Outcome, Is.EqualTo(BeginEditOutcome.Acquired));
            Assert.That(persisted.Status, Is.EqualTo(FailedMessageStatus.Resolved));
            Assert.That(metadata.ContainsKey(Constants.Documents.Metadata.Expires), Is.True);
        }
    }
}
