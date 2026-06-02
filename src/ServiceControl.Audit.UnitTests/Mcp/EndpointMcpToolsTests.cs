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
class EndpointMcpToolsTests
{
    StubAuditDataStore store = null!;
    EndpointTools tools = null!;

    [SetUp]
    public void SetUp()
    {
        store = new StubAuditDataStore();
        tools = new EndpointTools(store, NullLogger<EndpointTools>.Instance);
    }

    [Test]
    public async Task GetKnownEndpoints_returns_endpoints()
    {
        store.KnownEndpointsResult = new QueryResult<IList<KnownEndpointsView>>(
            [new() { EndpointDetails = new EndpointDetails { Name = "Sales", Host = "server1" } }],
            new QueryStatsInfo("etag", 1));

        var result = await tools.GetKnownEndpoints();

        Assert.That(result, Is.TypeOf<McpCollectionResult<KnownEndpointsView>>());
        Assert.That(result.TotalCount, Is.EqualTo(1));
        Assert.That(result.Results, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GetEndpointAuditCounts_returns_counts()
    {
        store.AuditCountsResult = new QueryResult<IList<AuditCount>>(
            [new() { UtcDate = DateTime.UtcNow.Date, Count = 42 }],
            new QueryStatsInfo("etag", 1));

        var result = await tools.GetEndpointAuditCounts("Sales");

        Assert.That(result, Is.TypeOf<McpCollectionResult<AuditCount>>());
        Assert.That(result.TotalCount, Is.EqualTo(1));
        Assert.That(store.LastAuditCountsEndpointName, Is.EqualTo("Sales"));
    }

    [TestCase(nameof(EndpointTools.GetKnownEndpoints))]
    [TestCase(nameof(EndpointTools.GetEndpointAuditCounts))]
    public void Structured_tools_use_structured_content(string methodName)
    {
        var method = typeof(EndpointTools).GetMethod(methodName)!;
        var attribute = (McpServerToolAttribute)Attribute.GetCustomAttribute(method, typeof(McpServerToolAttribute))!;

        Assert.That(attribute.UseStructuredContent, Is.True);
    }

    class StubAuditDataStore : IAuditDataStore
    {
        public QueryResult<IList<KnownEndpointsView>>? KnownEndpointsResult { get; set; }
        public QueryResult<IList<AuditCount>>? AuditCountsResult { get; set; }
        public string? LastAuditCountsEndpointName { get; private set; }

        public Task<QueryResult<IList<KnownEndpointsView>>> QueryKnownEndpoints(CancellationToken cancellationToken)
            => Task.FromResult(KnownEndpointsResult ?? new QueryResult<IList<KnownEndpointsView>>([], QueryStatsInfo.Zero));

        public Task<QueryResult<IList<AuditCount>>> QueryAuditCounts(string endpointName, CancellationToken cancellationToken)
        {
            LastAuditCountsEndpointName = endpointName;
            return Task.FromResult(AuditCountsResult ?? new QueryResult<IList<AuditCount>>([], QueryStatsInfo.Zero));
        }

        public Task<QueryResult<IList<MessagesView>>> GetMessages(bool includeSystemMessages, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new QueryResult<IList<MessagesView>>([], QueryStatsInfo.Zero));

        public Task<QueryResult<IList<MessagesView>>> QueryMessages(string searchParam, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new QueryResult<IList<MessagesView>>([], QueryStatsInfo.Zero));

        public Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpoint(bool includeSystemMessages, string endpointName, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new QueryResult<IList<MessagesView>>([], QueryStatsInfo.Zero));

        public Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpointAndKeyword(bool includeSystemMessages, string endpoint, string keyword, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new QueryResult<IList<MessagesView>>([], QueryStatsInfo.Zero));

        public Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpointAndKeyword(string endpoint, string keyword, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new QueryResult<IList<MessagesView>>([], QueryStatsInfo.Zero));

        public Task<QueryResult<IList<MessagesView>>> QueryMessagesByConversationId(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo, CancellationToken cancellationToken)
            => Task.FromResult(new QueryResult<IList<MessagesView>>([], QueryStatsInfo.Zero));

        public Task<MessageBodyView> GetMessageBody(string messageId, CancellationToken cancellationToken)
            => Task.FromResult(MessageBodyView.NotFound());

        public Task<QueryResult<SagaHistory>> QuerySagaHistoryById(Guid input, CancellationToken cancellationToken)
            => Task.FromResult(QueryResult<SagaHistory>.Empty());
    }
}
