#nullable enable
namespace ServiceControl.UnitTests.Recoverability;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NServiceBus.Testing;
using NUnit.Framework;
using ServiceControl.Infrastructure.Auth;
using ServiceControl.Persistence;
using ServiceControl.Recoverability;
using ServiceControl.Recoverability.API;
using ServiceControl.UnitTests.Operations;

[TestFixture]
public class FailureGroupsRetryControllerAuditTests
{
    [Test]
    public async Task Emits_group_retry_operation_entry()
    {
        var session = new TestableMessageSession();
        var audit = new RecordingMessageActionAuditLog();
        var user = new AuditUser("alice-sub", "Alice");
        var retryingManager = new RetryingManager(new FakeDomainEvents(), NullLogger<RetryingManager>.Instance);
        var controller = new FailureGroupsRetryController(session, retryingManager, new StubCurrentUserAccessor(user), audit);

        await controller.ArchiveGroupErrors("group-42");

        Assert.That(audit.Operations, Has.Count.EqualTo(1));
        var op = audit.Operations.Single();
        Assert.That(op.User, Is.EqualTo(user));
        Assert.That(op.Kind, Is.EqualTo(MessageActionKind.Retry));
        Assert.That(op.Scope, Is.EqualTo(MessageActionScope.Group));
        Assert.That(op.Resource, Is.EqualTo("group-42"));
        Assert.That(op.Permission, Is.EqualTo(Permissions.ErrorRecoverabilityGroupsRetry));
    }

    [Test]
    public async Task Group_retry_skipped_as_already_in_progress_is_not_audited()
    {
        var session = new TestableMessageSession();
        var audit = new RecordingMessageActionAuditLog();
        var retryingManager = new RetryingManager(new FakeDomainEvents(), NullLogger<RetryingManager>.Instance);
        await retryingManager.Preparing("group-42", RetryType.FailureGroup, totalNumberOfMessages: 10);
        var controller = new FailureGroupsRetryController(session, retryingManager, new StubCurrentUserAccessor(new AuditUser("alice-sub", "Alice")), audit);

        await controller.ArchiveGroupErrors("group-42");

        Assert.That(session.SentMessages, Is.Empty);
        Assert.That(audit.Operations, Is.Empty, "an ignored request must not be recorded as a successful operation");
    }
}
