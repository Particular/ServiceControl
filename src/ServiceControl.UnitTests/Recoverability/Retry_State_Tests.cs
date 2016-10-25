using NUnit.Framework;
using Raven.Client;
using ServiceControl.Contracts.Operations;
using ServiceControl.Infrastructure;
using ServiceControl.MessageFailures;
using ServiceControl.Recoverability;
using ServiceControl.UnitTests.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceControl.UnitTests.Recoverability
{
    [TestFixture]
    public class Retry_State_Tests
    {
        [Test]
        public void When_a_group_is_processed_it_is_set_to_the_Preparing_state()
        {
            var retryManager = new RetryOperationManager(new TestNotifier());

            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                CreateAFailedMessageAndMarkAsPartOfRetryBatch(documentStore, retryManager, "Test-group", true);
                var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);

                Assert.AreEqual(RetryState.Preparing, status.RetryState);
            }
        }

        [Test]
        public void When_a_group_is_prepared_and_SC_is_started_the_group_is_failed()
        {
            var retryManager = new RetryOperationManager(new TestNotifier());

            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                CreateAFailedMessageAndMarkAsPartOfRetryBatch(documentStore, retryManager, "Test-group", false);

                new RetryBatches_ByStatusAndSession().Execute(documentStore);
                new FailedMessageRetries_ByBatch().Execute(documentStore);

                var documentManager = new CustomRetryDocumentManager(false);
                documentManager.Store = documentStore;
                documentManager.RetryOperationManager = retryManager;

                var orphanage = new FailedMessageRetries.AdoptOrphanBatchesFromPreviousSession(documentManager, null, documentStore);
                orphanage.AdoptOrphanedBatches();

                var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);
                Assert.True(status.Failed);
            }
        }

        void CreateAFailedMessageAndMarkAsPartOfRetryBatch(IDocumentStore documentStore, RetryOperationManager retryManager, string groupId, bool progressToStaged)
        {
            var message = new FailedMessage
            {
                Id = FailedMessage.MakeDocumentId("Test-message-id"),
                UniqueMessageId = Guid.NewGuid().ToString(),
                FailureGroups = new List<FailedMessage.FailureGroup>
                    {
                        new FailedMessage.FailureGroup
                        {
                            Id = groupId,
                            Title = groupId,
                            Type = groupId
                        }
                    },
                Status = FailedMessageStatus.Unresolved,
                ProcessingAttempts = new List<FailedMessage.ProcessingAttempt>
                    {
                        new FailedMessage.ProcessingAttempt
                        {
                            AttemptedAt = DateTime.UtcNow,
                            MessageMetadata = new Dictionary<string, object>(),
                            FailureDetails = new FailureDetails()
                        }
                    }
            };

            using (var session = documentStore.OpenSession())
            {
                session.Store(message);
                session.SaveChanges();
            }

            new FailedMessages_ByGroup().Execute(documentStore);

            documentStore.WaitForIndexing();

            var documentManager = new CustomRetryDocumentManager(progressToStaged);
            var gateway = new RetriesGateway();

            documentManager.Store = documentStore;
            documentManager.RetryOperationManager = retryManager;

            gateway.Store = documentStore;
            gateway.RetryOperationManager = retryManager;
            gateway.RetryDocumentManager = documentManager;

            gateway.StartRetryForIndex<FailureGroupMessageView, FailedMessages_ByGroup>("Test-group", RetryType.FailureGroup, x => x.FailureGroupId == "Test-group", "Test-Context");

            documentStore.WaitForIndexing();

            gateway.ProcessNextBulkRetry();
        }
    }

    public class CustomRetryDocumentManager : RetryDocumentManager
    {
        private bool progressToStaged;

        public CustomRetryDocumentManager(bool progressToStaged)
            : base(new ShutdownNotifier())
        {
            RetrySessionId = Guid.NewGuid().ToString();
            this.progressToStaged = progressToStaged;
        }

        public override void MoveBatchToStaging(string batchDocumentId, string[] failedMessageRetryIds)
        {
            if (progressToStaged)
            {
                base.MoveBatchToStaging(batchDocumentId, failedMessageRetryIds);
            }
        }
    }
}