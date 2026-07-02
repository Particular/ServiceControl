#nullable enable
namespace ServiceControl.UnitTests.Recoverability;

using System.Collections.Generic;
using ServiceControl.Infrastructure.Auth;

sealed class RecordingMessageActionAuditLog : IMessageActionAuditLog
{
    public List<OperationEntry> Operations { get; } = [];
    public List<MessageEntry> Messages { get; } = [];

    public void Operation(AuditUser user, MessageActionKind kind, string permission, MessageActionScope scope, string? resource, int? count, string operationId, bool success = true) =>
        Operations.Add(new OperationEntry(user, kind, permission, scope, resource, count, operationId, success));

    public void MessageAction(AuditUser user, MessageActionKind kind, string permission, MessageActionScope scope, string messageId, string operationId, bool success = true) =>
        Messages.Add(new MessageEntry(user, kind, permission, scope, messageId, operationId, success));

    public sealed record OperationEntry(AuditUser User, MessageActionKind Kind, string Permission, MessageActionScope Scope, string? Resource, int? Count, string OperationId, bool Success);
    public sealed record MessageEntry(AuditUser User, MessageActionKind Kind, string Permission, MessageActionScope Scope, string MessageId, string OperationId, bool Success);
}
