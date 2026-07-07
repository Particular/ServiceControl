#nullable enable
namespace ServiceControl.UnitTests.MessageFailures;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CompositeViews.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using NServiceBus.Testing;
using NUnit.Framework;
using ServiceControl.EventLog;
using ServiceControl.Infrastructure.Auth;
using ServiceControl.MessageFailures;
using ServiceControl.MessageFailures.Api;
using ServiceControl.Operations;
using ServiceControl.Persistence;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Recoverability;
using ServiceControl.UnitTests.Recoverability;
using ServiceBus.Management.Infrastructure.Settings;

[TestFixture]
public class EditFailedMessagesControllerAuditTests
{
    static EditFailedMessagesController Create(StubErrorMessageDataStore store, RecordingMessageActionAuditLog audit, bool allowMessageEditing = true) =>
        new(new Settings { AllowMessageEditing = allowMessageEditing }, store, new TestableMessageSession(), NullLogger<EditFailedMessagesController>.Instance,
            new StubCurrentUserAccessor(new AuditUser("alice-sub", "Alice")), audit);

    static EditMessageModel ValidEdit() => new() { MessageBody = "body", MessageHeaders = [] };

    [Test]
    public async Task Edit_emits_single_operation()
    {
        var audit = new RecordingMessageActionAuditLog();
        var store = new StubErrorMessageDataStore { ErrorByResult = new FailedMessage { ProcessingAttempts = { new FailedMessage.ProcessingAttempt() } } };

        await Create(store, audit).Edit("msg-1", ValidEdit());

        var op = audit.Operations.Single();
        Assert.That(op.Kind, Is.EqualTo(MessageActionKind.Edit));
        Assert.That(op.Scope, Is.EqualTo(MessageActionScope.Single));
        Assert.That(op.Resource, Is.EqualTo("msg-1"));
        Assert.That(op.Count, Is.EqualTo(1));
    }

    sealed class FakeEditFailedMessagesManager : IEditFailedMessagesManager
    {
        public string? CurrentEditingRequestId { get; set; }

        public void Dispose()
        {
        }

        public Task SaveChanges() => Task.CompletedTask;
        public Task<FailedMessage> GetFailedMessage(string failedMessageId) => Task.FromResult<FailedMessage>(null!);
        public Task<string?> GetCurrentEditingRequestId(string failedMessageId) => Task.FromResult(CurrentEditingRequestId);
        public Task SetCurrentEditingRequestId(string editingMessageId) => Task.CompletedTask;
        public Task SetFailedMessageAsResolved() => Task.CompletedTask;
    }

    sealed class StubErrorMessageDataStore : IErrorMessageDataStore
    {
        public FailedMessage? ErrorByResult { get; set; }
        public FakeEditFailedMessagesManager EditManager { get; } = new();

        public Task<IEditFailedMessagesManager> CreateEditFailedMessageManager() => Task.FromResult<IEditFailedMessagesManager>(EditManager);
        public Task<FailedMessage> ErrorBy(string failedMessageId) => Task.FromResult(ErrorByResult!);

        public Task<QueryResult<IList<MessagesView>>> GetAllMessages(PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages, DateTimeRange? timeSentRange = null) => throw new NotImplementedException();
        public Task<QueryResult<IList<MessagesView>>> GetAllMessagesForEndpoint(string endpointName, PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages, DateTimeRange? timeSentRange = null) => throw new NotImplementedException();
        public Task<QueryResult<IList<MessagesView>>> GetAllMessagesByConversation(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages) => throw new NotImplementedException();
        public Task<QueryResult<IList<MessagesView>>> GetAllMessagesForSearch(string searchTerms, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null) => throw new NotImplementedException();
        public Task<QueryResult<IList<MessagesView>>> SearchEndpointMessages(string endpointName, string searchKeyword, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null) => throw new NotImplementedException();
        public Task FailedMessageMarkAsArchived(string failedMessageId) => throw new NotImplementedException();
        public Task<FailedMessage[]> FailedMessagesFetch(Guid[] ids) => throw new NotImplementedException();
        public Task StoreFailedErrorImport(FailedErrorImport failure) => throw new NotImplementedException();
        public Task<QueryResult<FailureGroupView>> GetFailureGroupView(string groupId, string status, string modified) => throw new NotImplementedException();
        public Task<IList<FailureGroupView>> GetFailureGroupsByClassifier(string classifier) => throw new NotImplementedException();
        public Task<QueryResult<IList<FailedMessageView>>> ErrorGet(string status, string modified, string queueAddress, PagingInfo pagingInfo, SortInfo sortInfo) => throw new NotImplementedException();
        public Task<QueryStatsInfo> ErrorsHead(string status, string modified, string queueAddress) => throw new NotImplementedException();
        public Task<QueryResult<IList<FailedMessageView>>> ErrorsByEndpointName(string status, string endpointName, string modified, PagingInfo pagingInfo, SortInfo sortInfo) => throw new NotImplementedException();
        public Task<IDictionary<string, object>> ErrorsSummary() => throw new NotImplementedException();
        public Task<FailedMessageView> ErrorLastBy(string failedMessageId) => throw new NotImplementedException();
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
        public Task<byte[]> FetchFromFailedMessage(string uniqueMessageId) => throw new NotImplementedException();
        public Task StoreEventLogItem(EventLogItem logItem) => throw new NotImplementedException();
        public Task StoreFailedMessagesForTestsOnly(params FailedMessage[] failedMessages) => throw new NotImplementedException();
    }
}
