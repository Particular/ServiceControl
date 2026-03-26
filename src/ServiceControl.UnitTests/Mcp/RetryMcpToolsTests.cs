#nullable enable

namespace ServiceControl.UnitTests.Mcp;

using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NServiceBus.Testing;
using NUnit.Framework;
using ServiceControl.Infrastructure.Mcp;
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
        tools = new RetryTools(messageSession, retryingManager, NullLogger<RetryTools>.Instance);
    }

    [Test]
    public async Task RetryFailedMessage_returns_accepted()
    {
        var result = await tools.RetryFailedMessage("msg-1");

        Assert.That(result.Status, Is.EqualTo(McpOperationStatus.Accepted));
        Assert.That(result.Message, Is.EqualTo("Retry requested for message 'msg-1'."));
        Assert.That(result.Error, Is.Null);
        Assert.That(messageSession.SentMessages, Has.Length.EqualTo(1));
    }

    [Test]
    public async Task RetryFailedMessages_returns_accepted()
    {
        var result = await tools.RetryFailedMessages(["msg-1", "msg-2"]);

        Assert.That(result.Status, Is.EqualTo(McpOperationStatus.Accepted));
        Assert.That(result.Message, Is.EqualTo("Retry requested for 2 messages."));
        Assert.That(result.Error, Is.Null);
        Assert.That(messageSession.SentMessages, Has.Length.EqualTo(1));
    }

    [Test]
    public async Task RetryFailedMessages_rejects_empty_ids()
    {
        var result = await tools.RetryFailedMessages(["msg-1", ""]);

        Assert.That(result.Status, Is.EqualTo(McpOperationStatus.ValidationError));
        Assert.That(result.Message, Is.Null);
        Assert.That(result.Error, Does.Contain("non-empty"));
    }

    [Test]
    public async Task RetryFailedMessagesByQueue_returns_accepted()
    {
        var result = await tools.RetryFailedMessagesByQueue("Sales@machine");

        Assert.That(result.Status, Is.EqualTo(McpOperationStatus.Accepted));
        Assert.That(result.Message, Is.EqualTo("Retry requested for all failed messages in queue 'Sales@machine'."));
        Assert.That(result.Error, Is.Null);
        Assert.That(messageSession.SentMessages, Has.Length.EqualTo(1));
    }

    [Test]
    public async Task RetryAllFailedMessages_returns_accepted()
    {
        var result = await tools.RetryAllFailedMessages();

        Assert.That(result.Status, Is.EqualTo(McpOperationStatus.Accepted));
        Assert.That(result.Message, Is.EqualTo("Retry requested for all failed messages."));
        Assert.That(result.Error, Is.Null);
        Assert.That(messageSession.SentMessages, Has.Length.EqualTo(1));
    }

    [Test]
    public async Task RetryAllFailedMessagesByEndpoint_returns_accepted()
    {
        var result = await tools.RetryAllFailedMessagesByEndpoint("Sales");

        Assert.That(result.Status, Is.EqualTo(McpOperationStatus.Accepted));
        Assert.That(result.Message, Is.EqualTo("Retry requested for all failed messages in endpoint 'Sales'."));
        Assert.That(result.Error, Is.Null);
    }

    [Test]
    public async Task RetryFailureGroup_returns_accepted()
    {
        var result = await tools.RetryFailureGroup("group-1");

        Assert.That(result.Status, Is.EqualTo(McpOperationStatus.Accepted));
        Assert.That(result.Message, Is.EqualTo("Retry requested for all messages in failure group 'group-1'."));
        Assert.That(result.Error, Is.Null);
    }

    [Test]
    public async Task RetryFailureGroup_returns_in_progress_when_already_running()
    {
        await retryingManager.Wait("group-1", RetryType.FailureGroup, System.DateTime.UtcNow);
        await retryingManager.Preparing("group-1", RetryType.FailureGroup, 1);

        var result = await tools.RetryFailureGroup("group-1");

        Assert.That(result.Status, Is.EqualTo(McpOperationStatus.InProgress));
        Assert.That(result.Message, Is.EqualTo("A retry operation is already in progress for group 'group-1'."));
        Assert.That(result.Error, Is.Null);
    }
}
