#nullable enable
namespace ServiceControl.Infrastructure.Auth.Rbac;

using Microsoft.Extensions.Logging;

/// <summary>
/// Logs every authorization decision as a structured log entry on category
/// <c>ServiceControl.Audit</c>.  The category is intentionally stable so
/// sinks can filter on it without coupling to the concrete type name.
/// </summary>
public sealed partial class AuthorizationAuditLog : IAuthorizationAuditLog
{
    const string AuditCategory = "ServiceControl.Audit";

    readonly ILogger logger;

    public AuthorizationAuditLog(ILoggerFactory loggerFactory)
    {
        logger = loggerFactory.CreateLogger(AuditCategory);
    }

    /// <inheritdoc />
    public void Decision(string subject, string permission, string? resource, bool allowed, string reason)
    {
        LogDecision(logger, subject, permission, resource, allowed ? "allow" : "deny", reason);
    }

    // Source-generated structured log method — zero allocation on the hot path.
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Authorization {Outcome}: subject={Subject} permission={Permission} resource={Resource} reason={Reason}")]
    static partial void LogDecision(
        ILogger logger,
        string subject,
        string permission,
        string? resource,
        string outcome,
        string reason);
}
