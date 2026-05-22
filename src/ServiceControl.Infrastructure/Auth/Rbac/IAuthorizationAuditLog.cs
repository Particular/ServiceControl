#nullable enable
namespace ServiceControl.Infrastructure.Auth.Rbac;

/// <summary>
/// Records every authorization allow/deny decision.
/// Implementations write structured log entries under the category
/// <c>ServiceControl.Audit</c> so they can be collected by any
/// <c>ILogger</c>-compatible sink (Seq, OTLP, in-memory test double, …).
/// </summary>
public interface IAuthorizationAuditLog
{
    /// <summary>
    /// Records a single authorization decision.
    /// </summary>
    /// <param name="subject">The identity of the principal (e.g. the <c>sub</c> claim).</param>
    /// <param name="permission">The permission that was evaluated (e.g. <c>messages:retry</c>).</param>
    /// <param name="resource">The specific resource checked, or <see langword="null"/> for verb-level checks.</param>
    /// <param name="allowed"><see langword="true"/> if the decision was allow; <see langword="false"/> for deny.</param>
    /// <param name="reason">A human-readable explanation (e.g. which policy rule matched, or why it didn't).</param>
    void Decision(string subject, string permission, string? resource, bool allowed, string reason);
}
