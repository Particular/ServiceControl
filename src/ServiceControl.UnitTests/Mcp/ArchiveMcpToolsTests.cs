#nullable enable

namespace ServiceControl.UnitTests.Mcp;

using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using NServiceBus.Testing;
using NUnit.Framework;
using ServiceControl.Mcp;
using ServiceControl.Persistence;
using ServiceControl.Persistence.Recoverability;
using ServiceControl.Recoverability;

[TestFixture]
class ArchiveMcpToolsTests
{
    TestableMessageSession messageSession = null!;
    StubArchiveMessages archiver = null!;
    ArchiveTools tools = null!;

    [SetUp]
    public void SetUp()
    {
        messageSession = new TestableMessageSession();
        archiver = new StubArchiveMessages();
        tools = new ArchiveTools(messageSession, archiver);
    }

    [Test]
    public async Task ArchiveFailedMessage_returns_accepted()
    {
        var result = await tools.ArchiveFailedMessage("msg-1");
        var response = JsonSerializer.Deserialize<McpStatusResponse>(result, JsonOptions)!;

        Assert.That(response.Status, Is.EqualTo("Accepted"));
        Assert.That(messageSession.SentMessages, Has.Length.EqualTo(1));
    }

    [Test]
    public async Task ArchiveFailedMessages_returns_accepted()
    {
        var result = await tools.ArchiveFailedMessages(["msg-1", "msg-2"]);
        var response = JsonSerializer.Deserialize<McpStatusResponse>(result, JsonOptions)!;

        Assert.That(response.Status, Is.EqualTo("Accepted"));
        Assert.That(messageSession.SentMessages, Has.Length.EqualTo(2));
    }

    [Test]
    public async Task ArchiveFailedMessages_rejects_empty_ids()
    {
        var result = await tools.ArchiveFailedMessages(["msg-1", ""]);
        var response = JsonSerializer.Deserialize<McpErrorResponse>(result, JsonOptions)!;

        Assert.That(response.Error, Does.Contain("non-empty"));
    }

    [Test]
    public async Task ArchiveFailureGroup_returns_accepted()
    {
        var result = await tools.ArchiveFailureGroup("group-1");
        var response = JsonSerializer.Deserialize<McpStatusResponse>(result, JsonOptions)!;

        Assert.That(response.Status, Is.EqualTo("Accepted"));
    }

    [Test]
    public async Task ArchiveFailureGroup_returns_in_progress_when_already_running()
    {
        archiver.OperationInProgress = true;

        var result = await tools.ArchiveFailureGroup("group-1");
        var response = JsonSerializer.Deserialize<McpStatusResponse>(result, JsonOptions)!;

        Assert.That(response.Status, Is.EqualTo("InProgress"));
    }

    [Test]
    public async Task UnarchiveFailedMessage_returns_accepted()
    {
        var result = await tools.UnarchiveFailedMessage("msg-1");
        var response = JsonSerializer.Deserialize<McpStatusResponse>(result, JsonOptions)!;

        Assert.That(response.Status, Is.EqualTo("Accepted"));
        Assert.That(messageSession.SentMessages, Has.Length.EqualTo(1));
    }

    [Test]
    public async Task UnarchiveFailedMessages_returns_accepted()
    {
        var result = await tools.UnarchiveFailedMessages(["msg-1", "msg-2"]);
        var response = JsonSerializer.Deserialize<McpStatusResponse>(result, JsonOptions)!;

        Assert.That(response.Status, Is.EqualTo("Accepted"));
    }

    [Test]
    public async Task UnarchiveFailedMessages_rejects_empty_ids()
    {
        var result = await tools.UnarchiveFailedMessages(["msg-1", ""]);
        var response = JsonSerializer.Deserialize<McpErrorResponse>(result, JsonOptions)!;

        Assert.That(response.Error, Does.Contain("non-empty"));
    }

    [Test]
    public async Task UnarchiveFailureGroup_returns_accepted()
    {
        var result = await tools.UnarchiveFailureGroup("group-1");
        var response = JsonSerializer.Deserialize<McpStatusResponse>(result, JsonOptions)!;

        Assert.That(response.Status, Is.EqualTo("Accepted"));
    }

    [Test]
    public async Task UnarchiveFailureGroup_returns_in_progress_when_already_running()
    {
        archiver.OperationInProgress = true;

        var result = await tools.UnarchiveFailureGroup("group-1");
        var response = JsonSerializer.Deserialize<McpStatusResponse>(result, JsonOptions)!;

        Assert.That(response.Status, Is.EqualTo("InProgress"));
    }

    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    class McpStatusResponse
    {
        public string? Status { get; set; }
        public string? Message { get; set; }
    }

    class McpErrorResponse
    {
        public string? Error { get; set; }
    }

    class StubArchiveMessages : IArchiveMessages
    {
        public bool OperationInProgress { get; set; }

        public bool IsOperationInProgressFor(string groupId, ArchiveType archiveType) => OperationInProgress;
        public bool IsArchiveInProgressFor(string groupId) => OperationInProgress;
        public Task StartArchiving(string groupId, ArchiveType archiveType) => Task.CompletedTask;
        public Task StartUnarchiving(string groupId, ArchiveType archiveType) => Task.CompletedTask;
        public Task ArchiveAllInGroup(string groupId) => Task.CompletedTask;
        public Task UnarchiveAllInGroup(string groupId) => Task.CompletedTask;
        public void DismissArchiveOperation(string groupId, ArchiveType archiveType) { }
        public IEnumerable<InMemoryArchive> GetArchivalOperations() => [];
    }
}
