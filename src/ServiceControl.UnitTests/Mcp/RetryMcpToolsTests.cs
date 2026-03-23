#nullable enable

namespace ServiceControl.UnitTests.Mcp;

using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NServiceBus.Testing;
using NUnit.Framework;
using ServiceControl.Mcp;
using ServiceControl.Persistence;
using ServiceControl.Recoverability;
using ServiceControl.UnitTests.Operations;

[TestFixture]
class RetryMcpToolsTests
{
    TestableMessageSession messageSession = null!;
    RetryingManager retryingManager = null!;
    RetryTools tools = null!;

    [SetUp]
    public void SetUp()
    {
        messageSession = new TestableMessageSession();
        retryingManager = new RetryingManager(new FakeDomainEvents(), NullLogger<RetryingManager>.Instance);
        tools = new RetryTools(messageSession, retryingManager);
    }

    [Test]
    public async Task RetryFailedMessage_returns_accepted()
    {
        var result = await tools.RetryFailedMessage("msg-1");
        var response = JsonSerializer.Deserialize<McpStatusResponse>(result, JsonOptions)!;

        Assert.That(response.Status, Is.EqualTo("Accepted"));
        Assert.That(messageSession.SentMessages, Has.Length.EqualTo(1));
    }

    [Test]
    public async Task RetryFailedMessages_returns_accepted()
    {
        var result = await tools.RetryFailedMessages(["msg-1", "msg-2"]);
        var response = JsonSerializer.Deserialize<McpStatusResponse>(result, JsonOptions)!;

        Assert.That(response.Status, Is.EqualTo("Accepted"));
        Assert.That(messageSession.SentMessages, Has.Length.EqualTo(1));
    }

    [Test]
    public async Task RetryFailedMessages_rejects_empty_ids()
    {
        var result = await tools.RetryFailedMessages(["msg-1", ""]);
        var response = JsonSerializer.Deserialize<McpErrorResponse>(result, JsonOptions)!;

        Assert.That(response.Error, Does.Contain("non-empty"));
    }

    [Test]
    public async Task RetryFailedMessagesByQueue_returns_accepted()
    {
        var result = await tools.RetryFailedMessagesByQueue("Sales@machine");
        var response = JsonSerializer.Deserialize<McpStatusResponse>(result, JsonOptions)!;

        Assert.That(response.Status, Is.EqualTo("Accepted"));
        Assert.That(messageSession.SentMessages, Has.Length.EqualTo(1));
    }

    [Test]
    public async Task RetryAllFailedMessages_returns_accepted()
    {
        var result = await tools.RetryAllFailedMessages();
        var response = JsonSerializer.Deserialize<McpStatusResponse>(result, JsonOptions)!;

        Assert.That(response.Status, Is.EqualTo("Accepted"));
        Assert.That(messageSession.SentMessages, Has.Length.EqualTo(1));
    }

    [Test]
    public async Task RetryAllFailedMessagesByEndpoint_returns_accepted()
    {
        var result = await tools.RetryAllFailedMessagesByEndpoint("Sales");
        var response = JsonSerializer.Deserialize<McpStatusResponse>(result, JsonOptions)!;

        Assert.That(response.Status, Is.EqualTo("Accepted"));
    }

    [Test]
    public async Task RetryFailureGroup_returns_accepted()
    {
        var result = await tools.RetryFailureGroup("group-1");
        var response = JsonSerializer.Deserialize<McpStatusResponse>(result, JsonOptions)!;

        Assert.That(response.Status, Is.EqualTo("Accepted"));
    }

    [Test]
    public async Task RetryFailureGroup_returns_in_progress_when_already_running()
    {
        await retryingManager.Wait("group-1", RetryType.FailureGroup, System.DateTime.UtcNow);
        await retryingManager.Preparing("group-1", RetryType.FailureGroup, 1);

        var result = await tools.RetryFailureGroup("group-1");
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
}
