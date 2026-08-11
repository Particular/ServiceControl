#nullable enable
namespace ServiceControl.UnitTests.MessageFailures;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        new(new Settings { AllowMessageEditing = allowMessageEditing }, store, store, new TestableMessageSession(), NullLogger<EditFailedMessagesController>.Instance,
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

        public Task SaveChanges() => Task.CompletedTask;
        public Task<FailedMessage?> GetFailedMessage(string failedMessageId) => Task.FromResult<FailedMessage?>(null);
        public Task<string?> GetCurrentEditingRequestId(string failedMessageId) => Task.FromResult(CurrentEditingRequestId);
        public Task SetCurrentEditingRequestId(string editingMessageId) => Task.CompletedTask;
        public Task SetFailedMessageAsResolved() => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    sealed class StubErrorMessageDataStore : IFailedMessageQueryDataStore, IEditFailedMessagesDataStore
    {
        public FailedMessage? ErrorByResult { get; set; }
        public FakeEditFailedMessagesManager EditManager { get; } = new();

        public Task<IEditFailedMessagesManager> CreateEditFailedMessageManager() => Task.FromResult<IEditFailedMessagesManager>(EditManager);
        public Task<FailedMessage?> GetFailedMessage(string failedMessageId, CancellationToken cancellationToken = default) => Task.FromResult(ErrorByResult);

        public Task<FailedMessage[]> GetFailedMessagesByIds(Guid[] ids, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<QueryResult<IList<FailedMessageView>>> GetFailedMessages(string? status, string? modified, string? queueAddress, PagingInfo pagingInfo, SortInfo sortInfo, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<QueryStatsInfo> GetFailedMessagesStats(string? status, string? modified, string? queueAddress, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<QueryResult<IList<FailedMessageView>>> GetFailedMessagesByEndpoint(string? status, string endpointName, string? modified, PagingInfo pagingInfo, SortInfo sortInfo, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IDictionary<string, object>> GetFailedMessagesSummary(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<FailedMessageView?> GetLatestFailedMessageView(string failedMessageId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
