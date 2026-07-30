#nullable enable
namespace ServiceControl.UnitTests.MessageFailures;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CompositeViews.Messages;
using NServiceBus;
using NServiceBus.Testing;
using NUnit.Framework;
using ServiceControl.EventLog;
using ServiceControl.Infrastructure;
using ServiceControl.Infrastructure.Auth;
using ServiceControl.MessageFailures;
using ServiceControl.MessageFailures.Api;
using ServiceControl.MessageFailures.Handlers;
using ServiceControl.MessageFailures.InternalMessages;
using ServiceControl.Operations;
using ServiceControl.Persistence;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Recoverability;
using ServiceControl.UnitTests.Operations;
using ServiceControl.UnitTests.Recoverability;

[TestFixture]
public class AsyncRangeAndQueueAuditTests
{
    static readonly AuditUser User = new("alice-sub", "Alice");

    static Dictionary<string, string> StampedHeaders(string operationId) => new()
    {
        [AuditHeaders.SubjectId] = User.Id,
        [AuditHeaders.SubjectName] = User.Name,
        [AuditHeaders.OperationId] = operationId
    };

    // Per-message retry entries are emitted at staging time (RetryProcessor.AuditStagedMessages),
    // once the message is really retried. The pending-retries handler only resolves ids, so it must
    // not audit them itself (a resolved message may still never be staged) — instead it forwards the
    // audit headers on the follow-up RetryMessagesById so the staged batch carries the attribution.

    [Test]
    public async Task PendingRetries_by_queue_forwards_attribution_to_the_staged_retry()
    {
        var store = new StubErrorMessageDataStore { RetryPendingMessagesResult = ["m-1", "m-2"] };
        var handler = new PendingRetriesHandler(store);

        var context = new TestableMessageHandlerContext { MessageHeaders = StampedHeaders("op-q") };
        await handler.Handle(new RetryPendingMessages { QueueAddress = "q", PeriodFrom = DateTime.UtcNow, PeriodTo = DateTime.UtcNow }, context);

        var sent = context.SentMessages.Single(m => m.Message is RetryMessagesById);
        var headers = sent.Options.GetHeaders();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(((RetryMessagesById)sent.Message).MessageUniqueIds, Is.EquivalentTo(new[] { "m-1", "m-2" }));
            Assert.That(headers[AuditHeaders.SubjectId], Is.EqualTo(User.Id));
            Assert.That(headers[AuditHeaders.SubjectName], Is.EqualTo(User.Name));
            Assert.That(headers[AuditHeaders.OperationId], Is.EqualTo("op-q"));
        }
    }

    [Test]
    public async Task PendingRetries_by_ids_forwards_attribution_to_the_staged_retry()
    {
        var handler = new PendingRetriesHandler(new StubErrorMessageDataStore());

        var context = new TestableMessageHandlerContext { MessageHeaders = StampedHeaders("op-pi") };
        await handler.Handle(new RetryPendingMessagesById { MessageUniqueIds = ["m-1", "m-2"] }, context);

        var sent = context.SentMessages.Single(m => m.Message is RetryMessagesById);
        var headers = sent.Options.GetHeaders();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(((RetryMessagesById)sent.Message).MessageUniqueIds, Is.EquivalentTo(new[] { "m-1", "m-2" }));
            Assert.That(headers[AuditHeaders.SubjectId], Is.EqualTo(User.Id));
            Assert.That(headers[AuditHeaders.SubjectName], Is.EqualTo(User.Name));
            Assert.That(headers[AuditHeaders.OperationId], Is.EqualTo("op-pi"));
        }
    }

    [Test]
    public async Task ArchiveMessage_audits_the_archived_message()
    {
        var audit = new RecordingMessageActionAuditLog();
        var store = new StubErrorMessageDataStore { ErrorByResult = new FailedMessage { Status = FailedMessageStatus.Unresolved } };
        var handler = new ArchiveMessageHandler(store, store, new FakeDomainEvents(), audit);

        var context = new TestableMessageHandlerContext { MessageHeaders = StampedHeaders("op-a") };
        await handler.Handle(new ArchiveMessage { FailedMessageId = "m-1" }, context);

        var msg = audit.Messages.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(msg.MessageId, Is.EqualTo("m-1"));
            Assert.That(msg.OperationId, Is.EqualTo("op-a"));
            Assert.That(msg.Kind, Is.EqualTo(MessageActionKind.Archive));
            Assert.That(msg.Scope, Is.EqualTo(MessageActionScope.Single));
        }
    }

    [Test]
    public async Task ArchiveMessage_already_archived_is_not_audited()
    {
        var audit = new RecordingMessageActionAuditLog();
        var store = new StubErrorMessageDataStore { ErrorByResult = new FailedMessage { Status = FailedMessageStatus.Archived } };
        var handler = new ArchiveMessageHandler(store, store, new FakeDomainEvents(), audit);

        var context = new TestableMessageHandlerContext { MessageHeaders = StampedHeaders("op-a") };
        await handler.Handle(new ArchiveMessage { FailedMessageId = "m-1" }, context);

        Assert.That(audit.Messages, Is.Empty);
    }

    [Test]
    public async Task UnArchiveMessages_audits_each_message_with_bare_id()
    {
        var audit = new RecordingMessageActionAuditLog();
        var store = new StubErrorMessageDataStore { UnArchiveMessagesResult = ["FailedMessages/m-1", "FailedMessages/m-2"] };
        var handler = new UnArchiveMessagesHandler(store, new FakeDomainEvents(), audit);

        var context = new TestableMessageHandlerContext { MessageHeaders = StampedHeaders("op-u") };
        await handler.Handle(new UnArchiveMessages { FailedMessageIds = ["m-1", "m-2"] }, context);

        Assert.That(audit.Messages.Select(m => m.MessageId), Is.EquivalentTo(new[] { "m-1", "m-2" }));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(audit.Messages, Has.All.Matches<RecordingMessageActionAuditLog.MessageEntry>(m => m.OperationId == "op-u"));
            Assert.That(audit.Messages, Has.All.Matches<RecordingMessageActionAuditLog.MessageEntry>(m => m.Kind == MessageActionKind.Unarchive));
            Assert.That(audit.Messages, Has.All.Matches<RecordingMessageActionAuditLog.MessageEntry>(m => m.Scope == MessageActionScope.Batch));
        }
    }

    [Test]
    public async Task Unarchive_by_range_audits_each_message_with_bare_id()
    {
        var audit = new RecordingMessageActionAuditLog();
        var store = new StubErrorMessageDataStore { UnArchiveByRangeResult = ["FailedMessages/m-1", "FailedMessages/m-2"] };
        var handler = new UnArchiveMessagesByRangeHandler(store, new FakeDomainEvents(), audit);

        var context = new TestableMessageHandlerContext { MessageHeaders = StampedHeaders("op-r") };
        await handler.Handle(new UnArchiveMessagesByRange { From = DateTime.UtcNow, To = DateTime.UtcNow }, context);

        Assert.That(audit.Messages.Select(m => m.MessageId), Is.EquivalentTo(new[] { "m-1", "m-2" }));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(audit.Messages, Has.All.Matches<RecordingMessageActionAuditLog.MessageEntry>(m => m.User.Equals(User)));
            Assert.That(audit.Messages, Has.All.Matches<RecordingMessageActionAuditLog.MessageEntry>(m => m.OperationId == "op-r"));
            Assert.That(audit.Messages, Has.All.Matches<RecordingMessageActionAuditLog.MessageEntry>(m => m.Kind == MessageActionKind.Unarchive));
            Assert.That(audit.Messages, Has.All.Matches<RecordingMessageActionAuditLog.MessageEntry>(m => m.Scope == MessageActionScope.Range));
        }
    }

    internal sealed class StubErrorMessageDataStore : IFailedMessageQueryDataStore, IFailedMessageLifecycleDataStore, IFailedMessageRetryDataStore
    {
        public string[] RetryPendingMessagesResult { get; set; } = [];
        public string[] UnArchiveByRangeResult { get; set; } = [];
        public string[] UnArchiveMessagesResult { get; set; } = [];
        public FailedMessage ErrorByResult { get; set; } = new();

        public Task<string[]> GetRetryPendingMessages(DateTime from, DateTime to, string queueAddress) => Task.FromResult(RetryPendingMessagesResult);
        public Task RemoveFailedMessageRetry(string uniqueMessageId) => Task.CompletedTask;
        public Task<string[]> UnArchiveMessagesByRange(DateTime from, DateTime to) => Task.FromResult(UnArchiveByRangeResult);
        public Task<string[]> UnArchiveMessages(IEnumerable<string> failedMessageIds) => Task.FromResult(UnArchiveMessagesResult);
        public Task<FailedMessage?> GetFailedMessage(string failedMessageId) => Task.FromResult<FailedMessage?>(ErrorByResult);
        public Task MarkAsArchived(string failedMessageId) => Task.CompletedTask;

        public Task<FailedMessage[]> GetFailedMessagesByIds(Guid[] ids) => throw new NotImplementedException();
        public Task<QueryResult<IList<FailedMessageView>>> GetFailedMessages(string? status, string? modified, string? queueAddress, PagingInfo pagingInfo, SortInfo sortInfo) => throw new NotImplementedException();
        public Task<QueryStatsInfo> GetFailedMessagesStats(string? status, string? modified, string? queueAddress) => throw new NotImplementedException();
        public Task<QueryResult<IList<FailedMessageView>>> GetFailedMessagesByEndpoint(string? status, string endpointName, string? modified, PagingInfo pagingInfo, SortInfo sortInfo) => throw new NotImplementedException();
        public Task<IDictionary<string, object>> GetFailedMessagesSummary() => throw new NotImplementedException();
        public Task<FailedMessageView?> GetLatestFailedMessageView(string failedMessageId) => throw new NotImplementedException();
        public Task<bool> MarkAsResolved(string failedMessageId) => throw new NotImplementedException();
        public Task ProcessPendingRetries(DateTime periodFrom, DateTime periodTo, string queueAddress, Func<string, Task> processCallback) => throw new NotImplementedException();
        public Task RevertRetry(string messageUniqueId) => throw new NotImplementedException();
        public Task<byte[]> GetFailedMessageBody(string uniqueMessageId) => throw new NotImplementedException();
    }
}
