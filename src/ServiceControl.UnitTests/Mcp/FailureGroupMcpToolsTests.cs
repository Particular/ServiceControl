#nullable enable

namespace ServiceControl.UnitTests.Mcp;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ServiceControl.Mcp;
using ServiceControl.Persistence;
using ServiceControl.Persistence.Recoverability;
using ServiceControl.Recoverability;
using ServiceControl.UnitTests.Operations;

[TestFixture]
class FailureGroupMcpToolsTests
{
    StubGroupsDataStore groupsStore = null!;
    StubRetryHistoryDataStore retryStore = null!;
    FailureGroupTools tools = null!;

    [SetUp]
    public void SetUp()
    {
        groupsStore = new StubGroupsDataStore();
        retryStore = new StubRetryHistoryDataStore();
        var domainEvents = new FakeDomainEvents();
        var retryingManager = new RetryingManager(domainEvents, NullLogger<RetryingManager>.Instance);
        var archiver = new StubArchiveMessages();
        var fetcher = new GroupFetcher(groupsStore, retryStore, retryingManager, archiver);
        tools = new FailureGroupTools(fetcher, retryStore, NullLogger<FailureGroupTools>.Instance);
    }

    [Test]
    public async Task GetFailureGroups_returns_groups()
    {
        groupsStore.FailureGroups =
        [
            new FailureGroupView { Id = "group-1", Title = "NullReferenceException", Type = "Exception Type and Stack Trace", Count = 5, First = DateTime.UtcNow.AddHours(-1), Last = DateTime.UtcNow }
        ];

        var result = await tools.GetFailureGroups();
        var response = JsonSerializer.Deserialize<List<GroupOperation>>(result, JsonOptions)!;

        Assert.That(response, Has.Count.EqualTo(1));
        Assert.That(response[0].Id, Is.EqualTo("group-1"));
        Assert.That(response[0].Count, Is.EqualTo(5));
    }

    [Test]
    public async Task GetFailureGroups_passes_classifier()
    {
        await tools.GetFailureGroups(classifier: "Message Type");

        Assert.That(groupsStore.LastClassifier, Is.EqualTo("Message Type"));
    }

    [Test]
    public async Task GetRetryHistory_returns_history()
    {
        retryStore.RetryHistoryResult = RetryHistory.CreateNew();

        var result = await tools.GetRetryHistory();
        var response = JsonSerializer.Deserialize<RetryHistory>(result, JsonOptions)!;

        Assert.That(response.HistoricOperations, Is.Empty);
        Assert.That(response.UnacknowledgedOperations, Is.Empty);
    }

    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    class StubGroupsDataStore : IGroupsDataStore
    {
        public IList<FailureGroupView> FailureGroups { get; set; } = [];
        public string? LastClassifier { get; private set; }

        public Task<IList<FailureGroupView>> GetFailureGroupsByClassifier(string classifier, string classifierFilter)
        {
            LastClassifier = classifier;
            return Task.FromResult(FailureGroups);
        }

        public Task<RetryBatch> GetCurrentForwardingBatch() => Task.FromResult<RetryBatch>(null!);
    }

    class StubRetryHistoryDataStore : IRetryHistoryDataStore
    {
        public RetryHistory? RetryHistoryResult { get; set; }

        public Task<RetryHistory> GetRetryHistory() => Task.FromResult(RetryHistoryResult ?? RetryHistory.CreateNew());
        public Task<bool> AcknowledgeRetryGroup(string groupId) => Task.FromResult(true);
        public Task RecordRetryOperationCompleted(string requestId, RetryType retryType, DateTime startTime, DateTime completionTime, string originator, string classifier, bool messageFailed, int numberOfMessagesProcessed, DateTime lastProcessed, int retryHistoryDepth) => Task.CompletedTask;
    }

    class StubArchiveMessages : IArchiveMessages
    {
        public bool IsOperationInProgressFor(string groupId, ArchiveType archiveType) => false;
        public bool IsArchiveInProgressFor(string groupId) => false;
        public Task StartArchiving(string groupId, ArchiveType archiveType) => Task.CompletedTask;
        public Task StartUnarchiving(string groupId, ArchiveType archiveType) => Task.CompletedTask;
        public Task ArchiveAllInGroup(string groupId) => Task.CompletedTask;
        public Task UnarchiveAllInGroup(string groupId) => Task.CompletedTask;
        public void DismissArchiveOperation(string groupId, ArchiveType archiveType) { }
        public IEnumerable<InMemoryArchive> GetArchivalOperations() => [];
    }
}
