#nullable enable
namespace ServiceControl.Infrastructure.Auth;

/// <summary>
/// Records user-initiated recoverability message actions (retry / archive / unarchive) as structured
/// audit entries. Operation-level entries answer "who did what to which resource"; per-message entries
/// record each affected message. Both are emitted on the stable <c>ServiceControl.Audit</c> category
/// family so SIEM sinks can collect them without coupling to the concrete type name.
/// </summary>
public interface IMessageActionAuditLog
{
    /// <summary>Records one user operation (a single click / API call), whatever its fan-out.</summary>
    void Operation(AuditUser user, MessageActionKind kind, string permission, MessageActionScope scope, string? resource, int? count, string operationId, bool success = true);

    /// <summary>Records one affected message, correlated to its operation via <paramref name="operationId"/>.</summary>
    void MessageAction(AuditUser user, MessageActionKind kind, string permission, MessageActionScope scope, string messageId, string operationId, bool success = true);
}
