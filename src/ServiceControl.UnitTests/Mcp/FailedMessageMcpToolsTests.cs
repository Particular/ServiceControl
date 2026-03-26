#nullable enable

namespace ServiceControl.UnitTests.Mcp;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;
using NUnit.Framework;
using ServiceControl.Contracts.Operations;
using ServiceControl.CompositeViews.Messages;
using ServiceControl.EventLog;
using ServiceControl.Infrastructure;
using ServiceControl.Infrastructure.Mcp;
using ServiceControl.MessageFailures;
using ServiceControl.MessageFailures.Api;
using ServiceControl.Mcp;
using ServiceControl.Operations;
using ServiceControl.Persistence;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Recoverability;

[TestFixture]
class FailedMessageMcpToolsTests
{
    StubErrorMessageDataStore store = null!;
    FailedMessageTools tools = null!;

    [SetUp]
    public void SetUp()
    {
        store = new StubErrorMessageDataStore();
        tools = new FailedMessageTools(store, NullLogger<FailedMessageTools>.Instance);
    }

    [Test]
    public async Task GetFailedMessages_returns_messages()
    {
        store.ErrorGetResult = new QueryResult<IList<FailedMessageView>>(
            [new() { Id = "msg-1", MessageType = "MyNamespace.MyMessage", Status = FailedMessageStatus.Unresolved }],
            new QueryStatsInfo("etag", 1, false));

        var result = await tools.GetFailedMessages();

        Assert.That(result, Is.TypeOf<McpCollectionResult<FailedMessageView>>());

        Assert.That(result.TotalCount, Is.EqualTo(1));
        Assert.That(result.Results, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GetFailedMessages_passes_paging_and_sort_parameters()
    {
        await tools.GetFailedMessages(page: 3, perPage: 10, sort: "time_sent", direction: "asc");

        Assert.That(store.LastErrorGetArgs, Is.Not.Null);
        Assert.That(store.LastErrorGetArgs!.Value.PagingInfo.Page, Is.EqualTo(3));
        Assert.That(store.LastErrorGetArgs!.Value.PagingInfo.PageSize, Is.EqualTo(10));
        Assert.That(store.LastErrorGetArgs!.Value.SortInfo.Sort, Is.EqualTo("time_sent"));
        Assert.That(store.LastErrorGetArgs!.Value.SortInfo.Direction, Is.EqualTo("asc"));
    }

    [Test]
    public async Task GetFailedMessages_passes_filter_parameters()
    {
        await tools.GetFailedMessages(status: "unresolved", modified: "2026-01-01", queueAddress: "Sales");

        Assert.That(store.LastErrorGetArgs!.Value.Status, Is.EqualTo("unresolved"));
        Assert.That(store.LastErrorGetArgs!.Value.Modified, Is.EqualTo("2026-01-01"));
        Assert.That(store.LastErrorGetArgs!.Value.QueueAddress, Is.EqualTo("Sales"));
    }

    [Test]
    public async Task GetFailedMessageById_returns_message()
    {
        store.ErrorByResult = new FailedMessage
        {
            Id = "msg-1",
            UniqueMessageId = "unique-1",
            Status = FailedMessageStatus.Unresolved,
            ProcessingAttempts =
            [
                new FailedMessage.ProcessingAttempt
                {
                    MessageId = "message-1",
                    Body = "body",
                    AttemptedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                    FailureDetails = new FailureDetails
                    {
                        AddressOfFailingEndpoint = "Sales",
                        Exception = new ExceptionDetails { ExceptionType = "System.Exception", Message = "boom" },
                        TimeOfFailure = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)
                    },
                    Headers = new Dictionary<string, string> { ["NServiceBus.MessageId"] = "message-1" },
                    MessageMetadata = new Dictionary<string, object> { ["Retries"] = 3 }
                }
            ],
            FailureGroups =
            [
                new FailedMessage.FailureGroup
                {
                    Id = "group-1",
                    Title = "Unhandled exception",
                    Type = "Exception"
                }
            ]
        };

        var result = await tools.GetFailedMessageById("msg-1");

        Assert.That(result, Is.TypeOf<McpFailedMessageResult>());

        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.Null);
            Assert.That(result.UniqueMessageId, Is.EqualTo("unique-1"));
            Assert.That(result.ProcessingAttempts, Has.Count.EqualTo(1));
            Assert.That(result.ProcessingAttempts[0].MessageId, Is.EqualTo("message-1"));
            Assert.That(result.ProcessingAttempts[0].MessageMetadata, Has.Count.EqualTo(1));
            Assert.That(result.ProcessingAttempts[0].MessageMetadata[0].Key, Is.EqualTo("Retries"));
            Assert.That(result.ProcessingAttempts[0].MessageMetadata[0].Value, Is.EqualTo("3"));
            Assert.That(result.ProcessingAttempts[0].MessageMetadata[0].Type, Is.EqualTo("integer"));
            Assert.That(result.FailureGroups, Has.Count.EqualTo(1));
            Assert.That(result.FailureGroups[0].Id, Is.EqualTo("group-1"));
        });
    }

    [Test]
    public async Task GetFailedMessageById_returns_error_when_not_found()
    {
        store.ErrorByResult = null;

        var result = await tools.GetFailedMessageById("msg-missing");

        Assert.That(result, Is.TypeOf<McpFailedMessageResult>());
        Assert.That(result.Error, Does.Contain("not found"));
    }

    [Test]
    public async Task GetFailedMessageLastAttempt_returns_view()
    {
        store.ErrorLastByResult = new FailedMessageView
        {
            Id = "msg-1",
            MessageType = "MyMessage",
            Status = FailedMessageStatus.Unresolved
        };

        var result = await tools.GetFailedMessageLastAttempt("msg-1");

        Assert.That(result, Is.TypeOf<McpFailedMessageViewResult>());
        Assert.That(result.Error, Is.Null);
        Assert.That(result.MessageType, Is.EqualTo("MyMessage"));
    }

    [Test]
    public async Task GetFailedMessageLastAttempt_returns_error_when_not_found()
    {
        store.ErrorLastByResult = null;

        var result = await tools.GetFailedMessageLastAttempt("msg-missing");

        Assert.That(result, Is.TypeOf<McpFailedMessageViewResult>());
        Assert.That(result.Error, Does.Contain("not found"));
    }

    [Test]
    public void GetFailedMessageById_returns_top_level_mcp_contract()
    {
        var method = typeof(FailedMessageTools).GetMethod(nameof(FailedMessageTools.GetFailedMessageById))!;

        Assert.That(method.ReturnType, Is.EqualTo(typeof(Task<McpFailedMessageResult>)));
    }

    [Test]
    public void GetFailedMessageLastAttempt_returns_top_level_mcp_contract()
    {
        var method = typeof(FailedMessageTools).GetMethod(nameof(FailedMessageTools.GetFailedMessageLastAttempt))!;

        Assert.That(method.ReturnType, Is.EqualTo(typeof(Task<McpFailedMessageViewResult>)));
    }

    [Test]
    public void Failed_message_detail_contract_uses_mcp_specific_nested_dtos()
    {
        Assert.Multiple(() =>
        {
            Assert.That(GetGenericListArgument(typeof(McpFailedMessageResult), nameof(McpFailedMessageResult.ProcessingAttempts)), Is.EqualTo(typeof(McpFailedProcessingAttemptResult)));
            Assert.That(GetGenericListArgument(typeof(McpFailedMessageResult), nameof(McpFailedMessageResult.FailureGroups)), Is.EqualTo(typeof(McpFailedFailureGroupResult)));
            Assert.That(GetGenericListArgument(typeof(McpFailedProcessingAttemptResult), nameof(McpFailedProcessingAttemptResult.MessageMetadata)), Is.EqualTo(typeof(McpMessageMetadataEntryResult)));
        });
    }

    [Test]
    public async Task GetErrorsSummary_returns_summary()
    {
        store.ErrorsSummaryResult = new Dictionary<string, object>
        {
            { "unresolved", 5 },
            { "archived", 3 }
        };

        var result = await tools.GetErrorsSummary();

        Assert.That(result, Is.TypeOf<McpErrorsSummaryResult>());
        Assert.That(result.Unresolved, Is.EqualTo(5L));
        Assert.That(result.Archived, Is.EqualTo(3L));
        Assert.That(result.Resolved, Is.EqualTo(0L));
        Assert.That(result.RetryIssued, Is.EqualTo(0L));
    }

    [Test]
    public async Task GetFailedMessagesByEndpoint_returns_messages()
    {
        store.ErrorsByEndpointResult = new QueryResult<IList<FailedMessageView>>(
            [new() { Id = "msg-1", MessageType = "MyMessage" }],
            new QueryStatsInfo("etag", 1, false));

        var result = await tools.GetFailedMessagesByEndpoint("Sales");

        Assert.That(result, Is.TypeOf<McpCollectionResult<FailedMessageView>>());
        Assert.That(result.TotalCount, Is.EqualTo(1));
        Assert.That(store.LastErrorsByEndpointName, Is.EqualTo("Sales"));
    }

    [TestCase(nameof(FailedMessageTools.GetFailedMessages))]
    [TestCase(nameof(FailedMessageTools.GetFailedMessageById))]
    [TestCase(nameof(FailedMessageTools.GetFailedMessageLastAttempt))]
    [TestCase(nameof(FailedMessageTools.GetErrorsSummary))]
    [TestCase(nameof(FailedMessageTools.GetFailedMessagesByEndpoint))]
    public void Structured_tools_use_structured_content(string methodName)
    {
        var method = typeof(FailedMessageTools).GetMethod(methodName)!;
        var attribute = (McpServerToolAttribute)Attribute.GetCustomAttribute(method, typeof(McpServerToolAttribute))!;

        Assert.That(attribute.UseStructuredContent, Is.True);
    }

    class StubErrorMessageDataStore : IErrorMessageDataStore
    {
        static readonly QueryResult<IList<FailedMessageView>> EmptyResult = new([], QueryStatsInfo.Zero);

        public QueryResult<IList<FailedMessageView>>? ErrorGetResult { get; set; }
        public QueryResult<IList<FailedMessageView>>? ErrorsByEndpointResult { get; set; }
        public FailedMessage? ErrorByResult { get; set; }
        public FailedMessageView? ErrorLastByResult { get; set; }
        public IDictionary<string, object>? ErrorsSummaryResult { get; set; }

        public (string? Status, string? Modified, string? QueueAddress, PagingInfo PagingInfo, SortInfo SortInfo)? LastErrorGetArgs { get; private set; }
        public string? LastErrorsByEndpointName { get; private set; }

        public Task<QueryResult<IList<FailedMessageView>>> ErrorGet(string status, string modified, string queueAddress, PagingInfo pagingInfo, SortInfo sortInfo)
        {
            LastErrorGetArgs = (status, modified, queueAddress, pagingInfo, sortInfo);
            return Task.FromResult(ErrorGetResult ?? EmptyResult);
        }

        public Task<FailedMessage> ErrorBy(string failedMessageId) => Task.FromResult(ErrorByResult)!;

        public Task<FailedMessageView> ErrorLastBy(string failedMessageId) => Task.FromResult(ErrorLastByResult)!;

        public Task<IDictionary<string, object>> ErrorsSummary() => Task.FromResult(ErrorsSummaryResult ?? new Dictionary<string, object>());

        public Task<QueryResult<IList<FailedMessageView>>> ErrorsByEndpointName(string status, string endpointName, string modified, PagingInfo pagingInfo, SortInfo sortInfo)
        {
            LastErrorsByEndpointName = endpointName;
            return Task.FromResult(ErrorsByEndpointResult ?? EmptyResult);
        }

        // Unused interface members
        public Task<QueryStatsInfo> ErrorsHead(string status, string modified, string queueAddress) => throw new NotImplementedException();
        public Task<QueryResult<IList<MessagesView>>> GetAllMessages(PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages, DateTimeRange? timeSentRange = null) => throw new NotImplementedException();
        public Task<QueryResult<IList<MessagesView>>> GetAllMessagesForEndpoint(string endpointName, PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages, DateTimeRange? timeSentRange = null) => throw new NotImplementedException();
        public Task<QueryResult<IList<MessagesView>>> GetAllMessagesByConversation(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages) => throw new NotImplementedException();
        public Task<QueryResult<IList<MessagesView>>> GetAllMessagesForSearch(string searchTerms, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null) => throw new NotImplementedException();
        public Task<QueryResult<IList<MessagesView>>> SearchEndpointMessages(string endpointName, string searchKeyword, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null) => throw new NotImplementedException();
        public Task FailedMessageMarkAsArchived(string failedMessageId) => throw new NotImplementedException();
        public Task<FailedMessage[]> FailedMessagesFetch(Guid[] ids) => throw new NotImplementedException();
        public Task StoreFailedErrorImport(FailedErrorImport failure) => throw new NotImplementedException();
        public Task<IEditFailedMessagesManager> CreateEditFailedMessageManager() => throw new NotImplementedException();
        public Task<QueryResult<FailureGroupView>> GetFailureGroupView(string groupId, string status, string modified) => throw new NotImplementedException();
        public Task<IList<FailureGroupView>> GetFailureGroupsByClassifier(string classifier) => throw new NotImplementedException();
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
        public Task<INotificationsManager> CreateNotificationsManager() => throw new NotImplementedException();
    }

    static Type GetGenericListArgument(Type declaringType, string propertyName) =>
        declaringType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!.PropertyType.GetGenericArguments().Single();
}
