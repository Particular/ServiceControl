#nullable enable
namespace ServiceControl.Persistence.Tests.Recoverability;

using System.Collections.Generic;
using ServiceControl.Infrastructure.Auth;

sealed class RecordingMessageActionAuditLog : IMessageActionAuditLog
{
    public List<MessageEntry> Messages { get; } = [];

    public void Operation(AuditUser user, MessageActionKind kind, string permission, MessageActionScope scope, string? resource, int? count, string operationId, bool success = true)
    {
    }

    public void MessageAction(AuditUser user, MessageActionKind kind, string permission, MessageActionScope scope, string messageId, string operationId, bool success = true) =>
        Messages.Add(new MessageEntry(user, kind, permission, scope, messageId, operationId, success));

    public sealed record MessageEntry(AuditUser User, MessageActionKind Kind, string Permission, MessageActionScope Scope, string MessageId, string OperationId, bool Success);
}
