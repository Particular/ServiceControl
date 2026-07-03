#nullable enable
namespace ServiceControl.Infrastructure.Auth;

using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

/// <summary>
/// Default <see cref="IAuthorizationAuditLog"/> that emits every decision as a structured log entry on
/// the stable category <c>ServiceControl.Audit</c>. Sinks filter on the category, not on the type name.
/// </summary>
public sealed partial class AuthorizationAuditLog(ILoggerFactory loggerFactory) : IAuthorizationAuditLog
{
    public const string AuditCategory = "ServiceControl.Audit"; // Logger name is used in logging configuration to write audit entries to a separate file.

    readonly ILogger logger = loggerFactory.CreateLogger(AuditCategory);

    // Relaxed escaping keeps the JSON readable for log sinks (no \uXXXX for '+', '<', accented names, …);
    // the HTML-safe default only matters in a browser context, which an audit log is not.
    static readonly JsonSerializerOptions EcsJsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public void Decision(string subjectId, string subjectName, string permission, string? resource, bool allowed, string reason)
    {
        ArgumentException.ThrowIfNullOrEmpty(subjectId);
        ArgumentException.ThrowIfNullOrEmpty(subjectName);
        ArgumentException.ThrowIfNullOrEmpty(permission);
        ArgumentException.ThrowIfNullOrEmpty(reason);

        var auditEvent = BuildEcsEvent(subjectId, subjectName, permission, resource, allowed, reason);

        if (allowed)
        {
            LogAllow(logger, auditEvent);
        }
        else
        {
            LogDeny(logger, auditEvent);
        }
    }

    // Serialises one authorization decision as an Elastic Common Schema (ECS) document so it ingests into
    // Elastic/Kibana — and most SIEMs — with no custom mapping. The schema is owned here, in the domain,
    // rather than in logging configuration. event.type/outcome carry the allow/deny; servicecontrol.* is the
    // app-specific namespace ECS reserves for custom fields.
    static string BuildEcsEvent(string subjectId, string subjectName, string permission, string? resource, bool allowed, string reason)
    {
        var ecs = new Dictionary<string, object?>
        {
            ["@timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
            ["event"] = new
            {
                kind = "event",
                category = new[] { "iam" },
                type = new[] { allowed ? "allowed" : "denied" },
                action = permission,
                outcome = allowed ? "success" : "failure"
            },
            ["user"] = new
            {
                id = subjectId,
                name = subjectName
            },
            ["servicecontrol"] = new
            {
                permission,
                resource,
                reason
            }
        };

        return JsonSerializer.Serialize(ecs, EcsJsonOptions);
    }

    // Source-generated structured log methods — the audit event is the pre-rendered ECS JSON document. Allow
    // and deny differ only by level so sinks can alert on denies (Warning) without parsing the payload.
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "{AuditEvent}")]
    static partial void LogAllow(ILogger logger, string auditEvent);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "{AuditEvent}")]
    static partial void LogDeny(ILogger logger, string auditEvent);
}
