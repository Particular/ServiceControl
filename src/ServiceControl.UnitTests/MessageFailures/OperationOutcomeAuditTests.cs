#nullable enable
namespace ServiceControl.UnitTests.MessageFailures;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Testing;
using NUnit.Framework;
using ServiceControl.Infrastructure.Auth;
using ServiceControl.MessageFailures.Api;
using ServiceControl.Recoverability.API;
using ServiceControl.UnitTests.Recoverability;

/// <summary>
/// The operation-level audit entry must record what actually happened: an action whose send throws
/// (broker down) returns a 500 to the caller and nothing was enqueued, so the trail must say
/// failure, not success.
/// </summary>
[TestFixture]
public class OperationOutcomeAuditTests
{
    static readonly AuditUser User = new("alice-sub", "Alice");

    [Test]
    public void Batch_archive_that_fails_to_send_is_recorded_as_failure()
    {
        var audit = new RecordingMessageActionAuditLog();
        var controller = new ArchiveMessagesController(new ThrowingMessageSession(), null!, new StubCurrentUserAccessor(User), audit);

        Assert.ThrowsAsync<InvalidOperationException>(() => controller.ArchiveBatch(["m-1", "m-2"]));

        var op = audit.Operations.Single();
        Assert.That(op.Success, Is.False, "an operation whose send failed must be recorded with a failure outcome");
    }

    [Test]
    public void Single_archive_that_fails_to_send_is_recorded_as_failure()
    {
        var audit = new RecordingMessageActionAuditLog();
        var controller = new ArchiveMessagesController(new ThrowingMessageSession(), null!, new StubCurrentUserAccessor(User), audit);

        Assert.ThrowsAsync<InvalidOperationException>(() => controller.Archive("m-1"));

        var op = audit.Operations.Single();
        Assert.That(op.Success, Is.False, "an operation whose send failed must be recorded with a failure outcome");
    }

    [Test]
    public void Group_archive_that_fails_to_send_is_recorded_as_failure()
    {
        var audit = new RecordingMessageActionAuditLog();
        var controller = new FailureGroupsArchiveController(new ThrowingMessageSession(), new NoopArchiveMessages(), new StubCurrentUserAccessor(User), audit);

        Assert.ThrowsAsync<InvalidOperationException>(() => controller.ArchiveGroupErrors("group-1"));

        var op = audit.Operations.Single();
        Assert.That(op.Success, Is.False, "an operation whose send failed must be recorded with a failure outcome");
    }

    sealed class ThrowingMessageSession : TestableMessageSession
    {
        public override Task Send(object message, SendOptions options, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("simulated transport failure");

        public override Task Send<T>(Action<T> messageConstructor, SendOptions options, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("simulated transport failure");
    }
}
