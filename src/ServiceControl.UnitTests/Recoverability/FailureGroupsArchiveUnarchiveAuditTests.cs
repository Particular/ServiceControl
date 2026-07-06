#nullable enable
namespace ServiceControl.UnitTests.Recoverability;

using System.Linq;
using System.Threading.Tasks;
using NServiceBus.Testing;
using NUnit.Framework;
using ServiceControl.Infrastructure.Auth;
using ServiceControl.Recoverability.API;

[TestFixture]
public class FailureGroupsArchiveUnarchiveAuditTests
{
    static readonly AuditUser User = new("alice-sub", "Alice");

    [Test]
    public async Task Archive_group_emits_operation_entry()
    {
        var audit = new RecordingMessageActionAuditLog();
        var controller = new FailureGroupsArchiveController(new TestableMessageSession(), new NoopArchiveMessages(), new StubCurrentUserAccessor(User), audit);

        await controller.ArchiveGroupErrors("group-7");

        var op = audit.Operations.Single();
        Assert.That(op.Kind, Is.EqualTo(MessageActionKind.Archive));
        Assert.That(op.Scope, Is.EqualTo(MessageActionScope.Group));
        Assert.That(op.Resource, Is.EqualTo("group-7"));
    }

    [Test]
    public async Task Unarchive_group_emits_operation_entry()
    {
        var audit = new RecordingMessageActionAuditLog();
        var controller = new FailureGroupsUnarchiveController(new TestableMessageSession(), new NoopArchiveMessages(), new StubCurrentUserAccessor(User), audit);

        await controller.UnarchiveGroupErrors("group-8");

        var op = audit.Operations.Single();
        Assert.That(op.Kind, Is.EqualTo(MessageActionKind.Unarchive));
        Assert.That(op.Scope, Is.EqualTo(MessageActionScope.Group));
        Assert.That(op.Resource, Is.EqualTo("group-8"));
    }

    [Test]
    public async Task Archive_group_skipped_as_already_in_progress_is_not_audited()
    {
        var audit = new RecordingMessageActionAuditLog();
        var session = new TestableMessageSession();
        var controller = new FailureGroupsArchiveController(session, new NoopArchiveMessages { OperationInProgress = true }, new StubCurrentUserAccessor(User), audit);

        await controller.ArchiveGroupErrors("group-7");

        Assert.That(session.SentMessages, Is.Empty);
        Assert.That(audit.Operations, Is.Empty, "an ignored request must not be recorded as a successful operation");
    }

    [Test]
    public async Task Unarchive_group_skipped_as_already_in_progress_is_not_audited()
    {
        var audit = new RecordingMessageActionAuditLog();
        var session = new TestableMessageSession();
        var controller = new FailureGroupsUnarchiveController(session, new NoopArchiveMessages { OperationInProgress = true }, new StubCurrentUserAccessor(User), audit);

        await controller.UnarchiveGroupErrors("group-8");

        Assert.That(session.SentMessages, Is.Empty);
        Assert.That(audit.Operations, Is.Empty, "an ignored request must not be recorded as a successful operation");
    }
}
