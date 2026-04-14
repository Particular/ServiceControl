#nullable enable

namespace ServiceControl.Audit.UnitTests.Mcp;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Audit.Auditing;
using Audit.Auditing.MessagesView;
using Audit.Infrastructure;
using Audit.Mcp;
using Audit.Monitoring;
using Audit.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;
using NUnit.Framework;
using ServiceControl.Infrastructure.Mcp;
using ServiceControl.SagaAudit;

[TestFixture]
class AuditMessageMcpToolsTests
{
    StubAuditDataStore store = null!;
    AuditMessageTools tools = null!;

    [SetUp]
    public void SetUp()
    {
        store = new StubAuditDataStore();
        tools = new AuditMessageTools(store, NullLogger<AuditMessageTools>.Instance);
    }

    [Test]
    public async Task GetAuditMessages_returns_messages()
    {
        store.MessagesResult = new QueryResult<IList<MessagesView>>(
            [new() { MessageId = "msg-1", MessageType = "MyNamespace.MyMessage" }],
            new QueryStatsInfo("etag", 1));

        var result = await tools.GetAuditMessages();

        Assert.That(result, Is.TypeOf<McpCollectionResult<MessagesView>>());
        Assert.That(result.TotalCount, Is.EqualTo(1));
        Assert.That(result.Results, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GetAuditMessages_passes_paging_and_sort_parameters()
    {
        await tools.GetAuditMessages(page: 2, perPage: 25, sort: "processed_at", direction: "asc");

        Assert.That(store.LastGetMessagesArgs, Is.Not.Null);
        Assert.That(store.LastGetMessagesArgs!.Value.PagingInfo.Page, Is.EqualTo(2));
        Assert.That(store.LastGetMessagesArgs!.Value.PagingInfo.PageSize, Is.EqualTo(25));
        Assert.That(store.LastGetMessagesArgs!.Value.SortInfo.Sort, Is.EqualTo("processed_at"));
        Assert.That(store.LastGetMessagesArgs!.Value.SortInfo.Direction, Is.EqualTo("asc"));
    }

    [Test]
    public async Task SearchAuditMessages_passes_query()
    {
        await tools.SearchAuditMessages("OrderPlaced");

        Assert.That(store.LastQueryMessagesSearchParam, Is.EqualTo("OrderPlaced"));
    }

    [Test]
    public async Task GetAuditMessagesByEndpoint_queries_by_endpoint()
    {
        await tools.GetAuditMessagesByEndpoint("Sales");

        Assert.That(store.LastQueryByEndpointName, Is.EqualTo("Sales"));
        Assert.That(store.LastQueryByEndpointKeyword, Is.Null);
    }

    [Test]
    public async Task GetAuditMessagesByEndpoint_with_keyword_uses_keyword_query()
    {
        await tools.GetAuditMessagesByEndpoint("Sales", keyword: "OrderPlaced");

        Assert.That(store.LastQueryByEndpointAndKeywordEndpoint, Is.EqualTo("Sales"));
        Assert.That(store.LastQueryByEndpointAndKeywordKeyword, Is.EqualTo("OrderPlaced"));
    }

    [Test]
    public async Task GetAuditMessagesByEndpoint_with_keyword_passes_includeSystemMessages()
    {
        await tools.GetAuditMessagesByEndpoint("Sales", keyword: "OrderPlaced", includeSystemMessages: true);

        Assert.That(store.LastQueryByEndpointAndKeywordIncludeSystemMessages, Is.True);
    }

    [Test]
    public async Task GetAuditMessagesByConversation_queries_by_conversation_id()
    {
        await tools.GetAuditMessagesByConversation("conv-123");

        Assert.That(store.LastConversationId, Is.EqualTo("conv-123"));
    }

    [Test]
    public async Task GetAuditMessageBody_returns_body_content()
    {
        store.MessageBodyResult = MessageBodyView.FromString("{\"orderId\": 123}", "application/json", 16, "etag");

        var result = await tools.GetAuditMessageBody("msg-1");

        Assert.That(result, Is.TypeOf<McpAuditMessageBodyResult>());
        Assert.That(result.ContentType, Is.EqualTo("application/json"));
        Assert.That(result.Body, Is.EqualTo("{\"orderId\": 123}"));
    }

    [Test]
    public async Task GetAuditMessageBody_returns_error_when_not_found()
    {
        store.MessageBodyResult = MessageBodyView.NotFound();

        var result = await tools.GetAuditMessageBody("msg-missing");

        Assert.That(result, Is.TypeOf<McpAuditMessageBodyResult>());
        Assert.That(result.Error, Does.Contain("not found"));
    }

    [Test]
    public async Task GetAuditMessageBody_returns_error_when_no_content()
    {
        store.MessageBodyResult = MessageBodyView.NoContent();

        var result = await tools.GetAuditMessageBody("msg-empty");

        Assert.That(result, Is.TypeOf<McpAuditMessageBodyResult>());
        Assert.That(result.Error, Does.Contain("no body content"));
    }

    [TestCase(nameof(AuditMessageTools.GetAuditMessages))]
    [TestCase(nameof(AuditMessageTools.SearchAuditMessages))]
    [TestCase(nameof(AuditMessageTools.GetAuditMessagesByEndpoint))]
    [TestCase(nameof(AuditMessageTools.GetAuditMessagesByConversation))]
    [TestCase(nameof(AuditMessageTools.GetAuditMessageBody))]
    public void Structured_tools_use_structured_content(string methodName)
    {
        var method = typeof(AuditMessageTools).GetMethod(methodName)!;
        var attribute = (McpServerToolAttribute)Attribute.GetCustomAttribute(method, typeof(McpServerToolAttribute))!;

        Assert.That(attribute.UseStructuredContent, Is.True);
    }

    class StubAuditDataStore : IAuditDataStore
    {
        static readonly QueryResult<IList<MessagesView>> EmptyMessagesResult = new([], QueryStatsInfo.Zero);
        static readonly QueryResult<IList<KnownEndpointsView>> EmptyEndpointsResult = new([], QueryStatsInfo.Zero);
        static readonly QueryResult<IList<AuditCount>> EmptyAuditCountsResult = new([], QueryStatsInfo.Zero);

        public QueryResult<IList<MessagesView>>? MessagesResult { get; set; }
        public MessageBodyView MessageBodyResult { get; set; } = MessageBodyView.NotFound();

        // Captured arguments
        public (bool IncludeSystemMessages, PagingInfo PagingInfo, SortInfo SortInfo, DateTimeRange? TimeSentRange)? LastGetMessagesArgs { get; private set; }
        public string? LastQueryMessagesSearchParam { get; private set; }
        public string? LastQueryByEndpointName { get; private set; }
        public string? LastQueryByEndpointKeyword { get; private set; }
        public string? LastQueryByEndpointAndKeywordEndpoint { get; private set; }
        public string? LastQueryByEndpointAndKeywordKeyword { get; private set; }
        public bool? LastQueryByEndpointAndKeywordIncludeSystemMessages { get; private set; }
        public string? LastConversationId { get; private set; }

        public Task<QueryResult<IList<MessagesView>>> GetMessages(bool includeSystemMessages, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default)
        {
            LastGetMessagesArgs = (includeSystemMessages, pagingInfo, sortInfo, timeSentRange);
            return Task.FromResult(MessagesResult ?? EmptyMessagesResult);
        }

        public Task<QueryResult<IList<MessagesView>>> QueryMessages(string searchParam, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default)
        {
            LastQueryMessagesSearchParam = searchParam;
            return Task.FromResult(MessagesResult ?? EmptyMessagesResult);
        }

        public Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpoint(bool includeSystemMessages, string endpointName, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default)
        {
            LastQueryByEndpointName = endpointName;
            LastQueryByEndpointKeyword = null;
            return Task.FromResult(MessagesResult ?? EmptyMessagesResult);
        }

        public Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpointAndKeyword(string endpoint, string keyword, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default)
            => QueryMessagesByReceivingEndpointAndKeyword(false, endpoint, keyword, pagingInfo, sortInfo, timeSentRange, cancellationToken);

        public Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpointAndKeyword(bool includeSystemMessages, string endpoint, string keyword, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default)
        {
            LastQueryByEndpointAndKeywordEndpoint = endpoint;
            LastQueryByEndpointAndKeywordKeyword = keyword;
            LastQueryByEndpointAndKeywordIncludeSystemMessages = includeSystemMessages;
            return Task.FromResult(MessagesResult ?? EmptyMessagesResult);
        }

        public Task<QueryResult<IList<MessagesView>>> QueryMessagesByConversationId(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo, CancellationToken cancellationToken)
        {
            LastConversationId = conversationId;
            return Task.FromResult(MessagesResult ?? EmptyMessagesResult);
        }

        public Task<MessageBodyView> GetMessageBody(string messageId, CancellationToken cancellationToken)
            => Task.FromResult(MessageBodyResult);

        public Task<QueryResult<IList<KnownEndpointsView>>> QueryKnownEndpoints(CancellationToken cancellationToken)
            => Task.FromResult(EmptyEndpointsResult);

        public Task<QueryResult<SagaHistory>> QuerySagaHistoryById(Guid input, CancellationToken cancellationToken)
            => Task.FromResult(QueryResult<SagaHistory>.Empty());

        public Task<QueryResult<IList<AuditCount>>> QueryAuditCounts(string endpointName, CancellationToken cancellationToken)
            => Task.FromResult(EmptyAuditCountsResult);
    }
}
