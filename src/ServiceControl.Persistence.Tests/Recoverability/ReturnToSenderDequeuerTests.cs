namespace ServiceControl.Persistence.Tests.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using EventLog;
    using MessageFailures;
    using MessageFailures.Api;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using NServiceBus.Extensibility;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using Persistence;
    using Persistence.Infrastructure;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Operations;
    using ServiceControl.Recoverability;

    [TestFixture]
    class ReturnToSenderDequeuerTests : PersistenceTestBase
    {
        MessageContext CreateMessage(string id, Dictionary<string, string> headers) =>
            new(
                id,
                headers,
                ReadOnlyMemory<byte>.Empty,
                new TransportTransaction(),
                "receiveAddress",
                new ContextBag()
            );

        public ReturnToSenderDequeuerTests() => RegisterServices = services => services.AddSingleton<ReturnToSender>();


        [Test]
        public async Task It_removes_staging_id_header()
        {
            var sender = new FakeSender();

            var headers = new Dictionary<string, string>
            {
                ["ServiceControl.Retry.StagingId"] = "SomeId",
                ["ServiceControl.TargetEndpointAddress"] = "TargetEndpoint",
            };
            var message = CreateMessage(Guid.NewGuid().ToString(), headers);

            await new ReturnToSender(null, NullLogger<ReturnToSender>.Instance).HandleMessage(message, sender, "error");

            Assert.That(sender.Message.Headers.ContainsKey("ServiceControl.Retry.StagingId"), Is.False);
        }

        [Test]
        public async Task It_fetches_the_body_from_storage_if_provided()
        {
            var sender = new FakeSender();

            var headers = new Dictionary<string, string>
            {
                ["ServiceControl.Retry.StagingId"] = "SomeId",
                ["ServiceControl.TargetEndpointAddress"] = "TargetEndpoint",
                ["ServiceControl.Retry.Attempt.MessageId"] = "MessageBodyId",
                ["ServiceControl.Retry.UniqueMessageId"] = "MessageBodyId"
            };
            var message = CreateMessage(Guid.NewGuid().ToString(), headers);

            await new ReturnToSender(new FakeErrorMessageDataStore(), NullLogger<ReturnToSender>.Instance).HandleMessage(message, sender, "error");

            Assert.That(Encoding.UTF8.GetString(sender.Message.Body.ToArray()), Is.EqualTo("MessageBodyId"));
        }

        [Test]
        public async Task It_uses_retry_to_if_provided()
        {
            var sender = new FakeSender();

            var headers = new Dictionary<string, string>
            {
                ["ServiceControl.Retry.StagingId"] = "SomeId",
                ["ServiceControl.TargetEndpointAddress"] = "TargetEndpoint",
                ["ServiceControl.RetryTo"] = "Proxy",
            };
            var message = CreateMessage(Guid.NewGuid().ToString(), headers);

            await new ReturnToSender(null, NullLogger<ReturnToSender>.Instance).HandleMessage(message, sender, "error");

            Assert.Multiple(() =>
            {
                Assert.That(sender.Destination, Is.EqualTo("Proxy"));
                Assert.That(sender.Message.Headers["ServiceControl.TargetEndpointAddress"], Is.EqualTo("TargetEndpoint"));
            });
        }

        [Test]
        public async Task It_sends_directly_to_target_if_retry_to_is_not_provided()
        {
            var sender = new FakeSender();

            var headers = new Dictionary<string, string>
            {
                ["ServiceControl.Retry.StagingId"] = "SomeId",
                ["ServiceControl.TargetEndpointAddress"] = "TargetEndpoint",
            };
            var message = CreateMessage(Guid.NewGuid().ToString(), headers);

            await new ReturnToSender(null, NullLogger<ReturnToSender>.Instance).HandleMessage(message, sender, "error");

            Assert.Multiple(() =>
            {
                Assert.That(sender.Destination, Is.EqualTo("TargetEndpoint"));
                Assert.That(sender.Message.Headers.ContainsKey("ServiceControl.TargetEndpointAddress"), Is.False);
            });
        }

        [Test]
        public async Task It_restores_body_id_and_target_addres_after_failure()
        {
            var sender = new FaultySender();

            var headers = new Dictionary<string, string>
            {
                ["ServiceControl.Retry.StagingId"] = "SomeId",
                ["ServiceControl.TargetEndpointAddress"] = "TargetEndpoint",
                ["ServiceControl.Retry.Attempt.MessageId"] = "MessageBodyId",
            };
            var message = CreateMessage(Guid.NewGuid().ToString(), headers);

            try
            {
                await new ReturnToSender(null, NullLogger<ReturnToSender>.Instance).HandleMessage(message, sender, "error");
            }
            catch (Exception)
            {
                //Intentionally empty catch
            }

            Assert.Multiple(() =>
            {
                Assert.That(message.Headers.ContainsKey("ServiceControl.TargetEndpointAddress"), Is.True);
                Assert.That(message.Headers.ContainsKey("ServiceControl.Retry.Attempt.MessageId"), Is.True);
            });
        }

        class FaultySender : IMessageDispatcher
        {
            public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, CancellationToken cancellationToken)
            {
                throw new Exception("Simulated");
            }
        }

        class FakeSender : IMessageDispatcher
        {
            public OutgoingMessage Message { get; private set; }
            public string Destination { get; private set; }


            public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, CancellationToken cancellationToken)
            {
                var operation = outgoingMessages.UnicastTransportOperations.Single();
                Message = operation.Message;
                Destination = operation.Destination;
                return Task.CompletedTask;
            }
        }

        class FakeErrorMessageDataStore : IErrorMessageDataStore
        {
            public Task<QueryResult<IList<MessagesView>>> GetAllMessages(PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages,
                DateTimeRange timeSentRange = null) =>
                throw new NotImplementedException();

            public Task<QueryResult<IList<MessagesView>>> GetAllMessagesForEndpoint(string endpointName, PagingInfo pagingInfo, SortInfo sortInfo,
                bool includeSystemMessages, DateTimeRange timeSentRange = null) =>
                throw new NotImplementedException();

            public Task<QueryResult<IList<MessagesView>>> GetAllMessagesByConversation(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo,
                bool includeSystemMessages) =>
                throw new NotImplementedException();

            public Task<QueryResult<IList<MessagesView>>> GetAllMessagesForSearch(string searchTerms, PagingInfo pagingInfo, SortInfo sortInfo,
                DateTimeRange timeSentRange = null) =>
                throw new NotImplementedException();

            public Task<QueryResult<IList<MessagesView>>> SearchEndpointMessages(string endpointName, string searchKeyword, PagingInfo pagingInfo, SortInfo sortInfo,
                DateTimeRange timeSentRange = null) =>
                throw new NotImplementedException();

            public Task FailedMessageMarkAsArchived(string failedMessageId) => throw new NotImplementedException();

            public Task<FailedMessage[]> FailedMessagesFetch(Guid[] ids) => throw new NotImplementedException();

            public Task StoreFailedErrorImport(FailedErrorImport failure) => throw new NotImplementedException();

            public Task<IEditFailedMessagesManager> CreateEditFailedMessageManager() => throw new NotImplementedException();

            public Task<QueryResult<FailureGroupView>> GetFailureGroupView(string groupId, string status, string modified) => throw new NotImplementedException();

            public Task<IList<FailureGroupView>> GetFailureGroupsByClassifier(string classifier) => throw new NotImplementedException();

            public Task<QueryResult<IList<FailedMessageView>>> ErrorGet(string status, string modified, string queueAddress, PagingInfo pagingInfo, SortInfo sortInfo) => throw new NotImplementedException();

            public Task<QueryStatsInfo> ErrorsHead(string status, string modified, string queueAddress) => throw new NotImplementedException();

            public Task<QueryResult<IList<FailedMessageView>>> ErrorsByEndpointName(string status, string endpointName, string modified, PagingInfo pagingInfo,
                SortInfo sortInfo) =>
                throw new NotImplementedException();

            public Task<IDictionary<string, object>> ErrorsSummary() => throw new NotImplementedException();

            public Task<FailedMessageView> ErrorLastBy(string failedMessageId) => throw new NotImplementedException();

            public Task<FailedMessage> ErrorBy(string failedMessageId) => throw new NotImplementedException();

            public Task<INotificationsManager> CreateNotificationsManager() => throw new NotImplementedException();

            public Task EditComment(string groupId, string comment) => throw new NotImplementedException();

            public Task DeleteComment(string groupId) => throw new NotImplementedException();

            public Task<QueryResult<IList<FailedMessageView>>> GetGroupErrors(string groupId, string status, string modified, SortInfo sortInfo, PagingInfo pagingInfo) => throw new NotImplementedException();

            public Task<QueryStatsInfo> GetGroupErrorsCount(string groupId, string status, string modified) => throw new NotImplementedException();

            public Task<QueryResult<IList<FailureGroupView>>> GetGroup(string groupId, string status, string modified) => throw new NotImplementedException();

            public Task<bool> MarkMessageAsResolved(string failedMessageId) => throw new NotImplementedException();

            public Task ProcessPendingRetries(DateTime periodFrom, DateTime periodTo, string queueAddress, Func<string, Task> processCallback) => throw new NotImplementedException();

            public Task<string[]> UnArchiveMessagesByRange(DateTime from, DateTime to) => throw new NotImplementedException();

            public Task<string[]> UnArchiveMessages(IEnumerable<string> failedMessageIds) => throw new NotImplementedException();

            public Task RevertRetry(string messageUniqueId) => throw new NotImplementedException();

            public Task RemoveFailedMessageRetryDocument(string uniqueMessageId) => throw new NotImplementedException();

            public Task<string[]> GetRetryPendingMessages(DateTime from, DateTime to, string queueAddress) => throw new NotImplementedException();

            public Task<byte[]> FetchFromFailedMessage(string bodyId) => Task.FromResult(Encoding.UTF8.GetBytes(bodyId));
            public Task StoreEventLogItem(EventLogItem logItem) => throw new NotImplementedException();

            public Task StoreFailedMessagesForTestsOnly(params FailedMessage[] failedMessages) => throw new NotImplementedException();
        }
    }
}