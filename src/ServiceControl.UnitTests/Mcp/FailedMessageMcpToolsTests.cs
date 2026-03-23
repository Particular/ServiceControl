#nullable enable

namespace ServiceControl.UnitTests.Mcp;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.CompositeViews.Messages;
using ServiceControl.EventLog;
using ServiceControl.Infrastructure;
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
        tools = new FailedMessageTools(store);
    }

    [Test]
    public async Task GetFailedMessages_returns_messages()
    {
        store.ErrorGetResult = new QueryResult<IList<FailedMessageView>>(
            [new() { Id = "msg-1", MessageType = "MyNamespace.MyMessage", Status = FailedMessageStatus.Unresolved }],
            new QueryStatsInfo("etag", 1, false));

        var result = await tools.GetFailedMessages();
        var response = JsonSerializer.Deserialize<McpToolResponse<FailedMessageView>>(result, JsonOptions)!;

        Assert.That(response.TotalCount, Is.EqualTo(1));
        Assert.That(response.Results, Has.Count.EqualTo(1));
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
            Status = FailedMessageStatus.Unresolved
        };

        var result = await tools.GetFailedMessageById("msg-1");
        var response = JsonSerializer.Deserialize<FailedMessage>(result, JsonOptions)!;

        Assert.That(response.UniqueMessageId, Is.EqualTo("unique-1"));
    }

    [Test]
    public async Task GetFailedMessageById_returns_error_when_not_found()
    {
        store.ErrorByResult = null;

        var result = await tools.GetFailedMessageById("msg-missing");
        var response = JsonSerializer.Deserialize<McpErrorResponse>(result, JsonOptions)!;

        Assert.That(response.Error, Does.Contain("not found"));
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
        var response = JsonSerializer.Deserialize<FailedMessageView>(result, JsonOptions)!;

        Assert.That(response.MessageType, Is.EqualTo("MyMessage"));
    }

    [Test]
    public async Task GetFailedMessageLastAttempt_returns_error_when_not_found()
    {
        store.ErrorLastByResult = null;

        var result = await tools.GetFailedMessageLastAttempt("msg-missing");
        var response = JsonSerializer.Deserialize<McpErrorResponse>(result, JsonOptions)!;

        Assert.That(response.Error, Does.Contain("not found"));
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
        var response = JsonSerializer.Deserialize<Dictionary<string, object>>(result, JsonOptions)!;

        Assert.That(response, Contains.Key("unresolved"));
        Assert.That(response, Contains.Key("archived"));
    }

    [Test]
    public async Task GetFailedMessagesByEndpoint_returns_messages()
    {
        store.ErrorsByEndpointResult = new QueryResult<IList<FailedMessageView>>(
            [new() { Id = "msg-1", MessageType = "MyMessage" }],
            new QueryStatsInfo("etag", 1, false));

        var result = await tools.GetFailedMessagesByEndpoint("Sales");
        var response = JsonSerializer.Deserialize<McpToolResponse<FailedMessageView>>(result, JsonOptions)!;

        Assert.That(response.TotalCount, Is.EqualTo(1));
        Assert.That(store.LastErrorsByEndpointName, Is.EqualTo("Sales"));
    }

    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    class McpToolResponse<T>
    {
        public int TotalCount { get; set; }
        public List<T> Results { get; set; } = [];
    }

    class McpErrorResponse
    {
        public string? Error { get; set; }
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
}
