#nullable enable
namespace ServiceControl.Infrastructure.Auth;

/// <summary>
/// Records every authorization allow/deny decision so the platform can demonstrate, after the fact,
/// who attempted what and how the system responded. Both allow and deny outcomes are captured —
/// denies alone are insufficient for most compliance use cases.
/// <para>
/// Implementations write structured log entries on a stable category so sinks (Seq, OTLP, file,
/// in-memory test double, …) can filter on it without coupling to the concrete type name.
/// </para>
/// </summary>
public interface IAuthorizationAuditLog
{
    /// <summary>
    /// Records a single authorization decision.
    /// </summary>
    /// <param name="subjectId">Stable identifier of the principal (e.g. the JWT <c>sub</c> claim). Must not be null or empty.</param>
    /// <param name="subjectName">Human-readable display name of the principal (e.g. <c>preferred_username</c>). Must not be null or empty.</param>
    /// <param name="permission">The permission that was evaluated (e.g. <c>error:messages:retry</c>).</param>
    /// <param name="resource">The specific resource checked, or <see langword="null"/> for verb-level checks.</param>
    /// <param name="allowed"><see langword="true"/> if the decision was allow; <see langword="false"/> for deny.</param>
    /// <param name="reason">A human-readable explanation (e.g. which role granted the permission, or why nothing matched).</param>
    void Decision(string subjectId, string subjectName, string permission, string? resource, bool allowed, string reason);
}
