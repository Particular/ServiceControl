namespace ServiceControl.Persistence.Tests.RavenDB.Recoverability
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client;
    using Raven.Client.Documents.Session;
    using NUnit.Framework;
    using Persistence.RavenDB;
    using ServiceControl.Persistence.Tests.RavenDB;

    // The retry documents are stored under a collection derived from their class name, so moving the
    // classes into this assembly must leave both the collection and the reads of documents already on
    // disk alone. Documents written before the move still carry the previous Raven-Clr-Type, which the
    // client only uses to pick a more derived type than the one asked for; a value it cannot resolve
    // leaves the requested type to deserialize the document.
    [TestFixture]
    class RetryDocumentCompatibilityTests : RavenPersistenceTestBase
    {
        const string PreviousBatchClrType = "ServiceControl.Persistence.RetryBatch, ServiceControl.Persistence";
        const string PreviousClaimClrType = "ServiceControl.Recoverability.FailedMessageRetry, ServiceControl.Persistence";

        [TestCase(typeof(RetryBatch), "RetryBatches")]
        [TestCase(typeof(RetryBatchNowForwarding), "RetryBatchNowForwardings")]
        [TestCase(typeof(FailedMessageRetry), "FailedMessageRetries")]
        public void Collections_are_unchanged(Type documentType, string collectionName) =>
            Assert.That(DocumentStore.Conventions.GetCollectionName(documentType), Is.EqualTo(collectionName));

        // Neither previous name can be mistaken for the document it used to name: the batch's now
        // names the contract type, which is not assignable to the document, and the claim's names
        // nothing at all. Either way the client falls back to the type being loaded.
        [TestCase(PreviousBatchClrType, typeof(Persistence.RetryBatch))]
        [TestCase(PreviousClaimClrType, null)]
        public void Previous_clr_type_names_no_longer_name_the_documents(string previousClrType, Type resolved) =>
            Assert.That(DocumentStore.Conventions.ResolveTypeFromClrTypeName(previousClrType), Is.EqualTo(resolved));

        [Test]
        public async Task Reads_and_forwards_a_batch_written_by_an_earlier_version()
        {
            const string batchId = "RetryBatches/written-by-an-earlier-version";
            var uniqueMessageId = Guid.NewGuid().ToString();

            using (var session = await SessionProvider.OpenSession())
            {
                var batch = new RetryBatch
                {
                    Id = batchId,
                    RequestId = "request-1",
                    RetryType = RetryType.MultipleMessages,
                    Status = RetryBatchStatus.Staging,
                    InitialBatchSize = 1,
                    StartTime = DateTime.UtcNow,
                    FailureRetries = [RetryDocumentDataStore.MakeFailedMessageRetriesDocumentId(uniqueMessageId)]
                };

                await session.StoreAsync(batch);

                StoredAs(session, batch, "RetryBatches", PreviousBatchClrType);

                await session.SaveChangesAsync();
            }

            await CompleteDatabaseOperation();
            await AssertStoredAs<RetryBatch>(batchId, "RetryBatches", PreviousBatchClrType);

            var staging = await RetryStagingStore.GetStagingBatch();

            Assert.That(staging?.Id, Is.EqualTo(batchId));

            await RetryStagingStore.MarkBatchAsForwarding(batchId, "staging-1", [uniqueMessageId]);
            await CompleteDatabaseOperation();

            await AssertStoredAs<RetryBatch>(batchId, "RetryBatches", PreviousBatchClrType);

            var forwarding = await RetryStagingStore.GetBatch(batchId, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await RetryStagingStore.GetForwardingBatchId(), Is.EqualTo(batchId));
                Assert.That(forwarding.StagingId, Is.EqualTo("staging-1"));
            }
        }

        [Test]
        public async Task Stages_a_message_claimed_by_an_earlier_version()
        {
            const string batchId = "RetryBatches/claimed-by-an-earlier-version";

            var failure = new IngestedFailure();
            var message = failure.ToFailedMessage();
            message.Id = PersistenceTestsContext.GenerateFailedMessageRecordId(message.UniqueMessageId);

            await PersistenceTestsContext.InsertFailedMessages(message);

            var claimId = RetryDocumentDataStore.MakeFailedMessageRetriesDocumentId(failure.UniqueMessageIdString);

            using (var session = await SessionProvider.OpenSession())
            {
                var batch = new RetryBatch
                {
                    Id = batchId,
                    RequestId = "request-1",
                    RetryType = RetryType.MultipleMessages,
                    Status = RetryBatchStatus.Staging,
                    InitialBatchSize = 1,
                    StartTime = DateTime.UtcNow,
                    FailureRetries = [claimId]
                };

                var claim = new FailedMessageRetry
                {
                    Id = claimId,
                    FailedMessageId = message.Id,
                    RetryBatchId = batchId
                };

                await session.StoreAsync(batch);
                await session.StoreAsync(claim);

                StoredAs(session, batch, "RetryBatches", PreviousBatchClrType);
                StoredAs(session, claim, "FailedMessageRetries", PreviousClaimClrType);

                await session.SaveChangesAsync();
            }

            await CompleteDatabaseOperation();
            await AssertStoredAs<FailedMessageRetry>(claimId, "FailedMessageRetries", PreviousClaimClrType);

            var messagesToStage = await RetryStagingStore.GetMessagesToStage(batchId);

            Assert.That(messagesToStage.Single().UniqueMessageId, Is.EqualTo(failure.UniqueMessageIdString));
        }

        static void StoredAs(IAsyncDocumentSession session, object document, string collection, string clrType)
        {
            var metadata = session.Advanced.GetMetadataFor(document);

            metadata[Constants.Documents.Metadata.Collection] = collection;
            metadata[Constants.Documents.Metadata.RavenClrType] = clrType;
        }

        async Task AssertStoredAs<T>(string documentId, string collection, string clrType)
        {
            using var session = await SessionProvider.OpenSession();

            var metadata = session.Advanced.GetMetadataFor(await session.LoadAsync<T>(documentId));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(metadata[Constants.Documents.Metadata.Collection], Is.EqualTo(collection));
                Assert.That(metadata[Constants.Documents.Metadata.RavenClrType], Is.EqualTo(clrType));
            }
        }
    }
}
