#nullable enable
namespace ServiceControl.UnitTests.Recoverability;

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NServiceBus.Testing;
using NUnit.Framework;
using ServiceControl.Infrastructure.Auth;
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
}
