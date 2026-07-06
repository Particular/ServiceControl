#nullable enable
namespace ServiceControl.UnitTests.MessageFailures;

using System.Linq;
using System.Threading.Tasks;
using NServiceBus.Testing;
using NUnit.Framework;
using ServiceControl.Infrastructure.Auth;
using ServiceControl.MessageFailures;
using ServiceControl.MessageFailures.Api;
using ServiceControl.MessageFailures.Handlers;
using ServiceControl.MessageFailures.InternalMessages;
using ServiceControl.UnitTests.Operations;
using ServiceControl.UnitTests.Recoverability;

/// <summary>
/// A batch archive fans out into one ArchiveMessage command per id. The per-message audit entries
/// must carry the originating operation's scope — like every other path (unarchive batch/range,
/// group, retry) — not a hardcoded "single".
/// </summary>
[TestFixture]
public class ArchiveScopeAuditTests
{
    static readonly AuditUser User = new("alice-sub", "Alice");

    [Test]
    public async Task Batch_archive_commands_carry_the_batch_scope()
    {
        var session = new TestableMessageSession();
        var controller = new ArchiveMessagesController(session, null!, new StubCurrentUserAccessor(User), new RecordingMessageActionAuditLog());

        await controller.ArchiveBatch(["m-1", "m-2"]);

        var scopes = session.SentMessages.Select(s => ((ArchiveMessage)s.Message).Scope).ToArray();
        Assert.That(scopes, Has.Length.EqualTo(2).And.All.EqualTo(MessageActionScope.Batch));
    }

    [Test]
    public async Task Single_archive_command_carries_the_single_scope()
    {
        var session = new TestableMessageSession();
        var controller = new ArchiveMessagesController(session, null!, new StubCurrentUserAccessor(User), new RecordingMessageActionAuditLog());

        await controller.Archive("m-1");

        Assert.That(((ArchiveMessage)session.SentMessages.Single().Message).Scope, Is.EqualTo(MessageActionScope.Single));
    }

    [Test]
    public async Task Archived_message_is_audited_with_the_scope_of_the_originating_operation()
    {
        var audit = new RecordingMessageActionAuditLog();
        var store = new AsyncRangeAndQueueAuditTests.StubErrorMessageDataStore { ErrorByResult = new FailedMessage { Status = FailedMessageStatus.Unresolved } };
        var handler = new ArchiveMessageHandler(store, new FakeDomainEvents(), audit);

        var context = new TestableMessageHandlerContext
        {
            MessageHeaders =
            {
                [AuditHeaders.SubjectId] = User.Id,
                [AuditHeaders.SubjectName] = User.Name,
                [AuditHeaders.OperationId] = "op-batch"
            }
        };
        await handler.Handle(new ArchiveMessage { FailedMessageId = "m-1", Scope = MessageActionScope.Batch }, context);

        Assert.That(audit.Messages.Single().Scope, Is.EqualTo(MessageActionScope.Batch));
    }
}
