#nullable enable
namespace ServiceControl.Infrastructure.Auth;

using System;
using Microsoft.Extensions.Logging;

/// <summary>
/// Default <see cref="IAuthorizationAuditLog"/> that emits every decision as a structured log entry on
/// the stable category <c>ServiceControl.Audit</c>. Sinks filter on the category, not on the type name.
/// </summary>
public sealed partial class AuthorizationAuditLog(ILoggerFactory loggerFactory) : IAuthorizationAuditLog
{
    const string AuditCategory = "ServiceControl.Audit";

    readonly ILogger logger = loggerFactory.CreateLogger(AuditCategory);

    public void Decision(string subjectId, string subjectName, string permission, string? resource, bool allowed, string reason)
    {
        ArgumentException.ThrowIfNullOrEmpty(subjectId);
        ArgumentException.ThrowIfNullOrEmpty(subjectName);
        ArgumentException.ThrowIfNullOrEmpty(permission);
        ArgumentException.ThrowIfNullOrEmpty(reason);

        if(allowed)
        {
            LogAllow(logger, subjectId, subjectName, permission, resource, reason);
        }
        else
        {
            LogDeny(logger, subjectId, subjectName, permission, resource, reason);
        }
    }

    // Source-generated structured log method — zero allocation on the hot path.
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Allow: subjectId={SubjectId} subjectName={SubjectName} permission={Permission} resource={Resource} reason={Reason}")]
    static partial void LogAllow(
        ILogger logger,
        string subjectId,
        string subjectName,
        string permission,
        string? resource,
        string reason
        );

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Deny: subjectId={SubjectId} subjectName={SubjectName} permission={Permission} resource={Resource} reason={Reason}")]
    static partial void LogDeny(
        ILogger logger,
        string subjectId,
        string subjectName,
        string permission,
        string? resource,
        string reason
        );
}
