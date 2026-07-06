#nullable enable
namespace ServiceControl.Infrastructure.Auth;

using System;
using System.Threading.Tasks;

public static class MessageActionAuditLogExtensions
{
    /// <summary>
    /// Executes a message action and records the operation-level audit entry with the actual
    /// outcome: success when the action completed, failure when it threw (the exception is
    /// rethrown). Logging after the action keeps the trail truthful — an entry written before the
    /// send would claim success for an operation the transport may have rejected.
    /// </summary>
    public static async Task AuditedOperation(this IMessageActionAuditLog auditLog, AuditUser user,
        MessageActionKind kind, string permission, MessageActionScope scope, string? resource,
        int? count, string operationId, Func<Task> action)
    {
        var success = true;
        try
        {
            await action().ConfigureAwait(false);
        }
        catch
        {
            success = false;
            throw;
        }
        finally
        {
            auditLog.Operation(user, kind, permission, scope, resource, count, operationId, success);
        }
    }
}
