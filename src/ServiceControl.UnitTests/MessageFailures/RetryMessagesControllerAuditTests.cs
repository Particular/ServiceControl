#nullable enable
namespace ServiceControl.UnitTests.MessageFailures;

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NServiceBus.Testing;
using NUnit.Framework;
using ServiceControl.Infrastructure.Auth;
using ServiceControl.MessageFailures.Api;
using ServiceControl.UnitTests.Recoverability;
using ServiceBus.Management.Infrastructure.Settings;

[TestFixture]
public class RetryMessagesControllerAuditTests
{
    static RetryMessagesController Create(TestableMessageSession session, RecordingMessageActionAuditLog audit) =>
        new(new Settings(), null, null, session, NullLogger<RetryMessagesController>.Instance,
            new StubCurrentUserAccessor(new AuditUser("alice-sub", "Alice")), audit);

    [Test]
    public async Task RetryMessageBy_local_emits_single_operation()
    {
        var audit = new RecordingMessageActionAuditLog();
        await Create(new TestableMessageSession(), audit).RetryMessageBy(null, "msg-1");

        var op = audit.Operations.Single();
        Assert.That(op.Kind, Is.EqualTo(MessageActionKind.Retry));
        Assert.That(op.Scope, Is.EqualTo(MessageActionScope.Single));
        Assert.That(op.Resource, Is.EqualTo("msg-1"));
        Assert.That(op.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task RetryAllBy_ids_emits_batch_operation()
    {
        var audit = new RecordingMessageActionAuditLog();
        await Create(new TestableMessageSession(), audit).RetryAllBy(["m-1", "m-2"]);

        var op = audit.Operations.Single();
        Assert.That(op.Kind, Is.EqualTo(MessageActionKind.Retry));
        Assert.That(op.Scope, Is.EqualTo(MessageActionScope.Batch));
        Assert.That(op.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task RetryAllBy_queueAddress_emits_queue_operation()
    {
        var audit = new RecordingMessageActionAuditLog();
        await Create(new TestableMessageSession(), audit).RetryAllBy("queue-a");

        var op = audit.Operations.Single();
        Assert.That(op.Kind, Is.EqualTo(MessageActionKind.Retry));
        Assert.That(op.Scope, Is.EqualTo(MessageActionScope.Queue));
        Assert.That(op.Resource, Is.EqualTo("queue-a"));
        Assert.That(op.Count, Is.Null);
    }

    [Test]
    public async Task RetryAll_emits_all_scope_operation()
    {
        var audit = new RecordingMessageActionAuditLog();
        await Create(new TestableMessageSession(), audit).RetryAll();

        var op = audit.Operations.Single();
        Assert.That(op.Kind, Is.EqualTo(MessageActionKind.Retry));
        Assert.That(op.Scope, Is.EqualTo(MessageActionScope.All));
        Assert.That(op.Resource, Is.Null);
        Assert.That(op.Count, Is.Null);
    }

    [Test]
    public async Task RetryAllByEndpoint_emits_endpoint_operation()
    {
        var audit = new RecordingMessageActionAuditLog();
        await Create(new TestableMessageSession(), audit).RetryAllByEndpoint("endpoint-a");

        var op = audit.Operations.Single();
        Assert.That(op.Kind, Is.EqualTo(MessageActionKind.Retry));
        Assert.That(op.Scope, Is.EqualTo(MessageActionScope.Endpoint));
        Assert.That(op.Resource, Is.EqualTo("endpoint-a"));
        Assert.That(op.Count, Is.Null);
    }
}
