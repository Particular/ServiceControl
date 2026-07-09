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
public sealed class AuthorizationAuditLog(ILoggerFactory loggerFactory) : IAuthorizationAuditLog
{
    public const string AuditCategory = "ServiceControl.Audit"; // Logger name is used in logging configuration to write audit entries to a separate file.

    // The ECS version the emitted documents conform to, surfaced as the ecs.version field so downstream
    // pipelines can pick the matching mappings. 8.11.0 is the latest ECS schema release; the fields used
    // here (event.category/type/outcome, user.*) are stable across the 8.x line. Shared with
    // MessageActionAuditLog so both streams declare the same version.
    internal const string EcsVersion = "8.11.0";

    readonly ILogger logger = loggerFactory.CreateLogger(AuditCategory);

    // Relaxed escaping keeps the JSON readable for log sinks (no \uXXXX for '+', '<', accented names, …);
    // the HTML-safe default only matters in a browser context, which an audit log is not. Shared with
    // MessageActionAuditLog so both ECS streams keep the same serialization contract.
    internal static readonly JsonSerializerOptions EcsJsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public void Decision(string subjectId, string subjectName, string permission, string? resource, bool allowed, string reason, IReadOnlyCollection<string>? roles = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(subjectId);
        ArgumentException.ThrowIfNullOrEmpty(subjectName);
        ArgumentException.ThrowIfNullOrEmpty(permission);
        ArgumentException.ThrowIfNullOrEmpty(reason);

        var level = allowed ? LogLevel.Information : LogLevel.Warning;
        if (!logger.IsEnabled(level))
        {
            return;
        }

        var auditEvent = BuildEcsEvent(subjectId, subjectName, permission, resource, allowed, reason, roles);
        logger.Log(level, allowed ? AllowEventId : DenyEventId, auditEvent, null, IdentityFormatter);
    }

    // Serialises one authorization decision as an Elastic Common Schema (ECS) document so it ingests into
    // Elastic/Kibana — and most SIEMs — with no custom mapping. The schema is owned here, in the domain,
    // rather than in logging configuration. event.type/outcome carry the allow/deny; servicecontrol.* is the
    // app-specific namespace ECS reserves for custom fields.
    static string BuildEcsEvent(string subjectId, string subjectName, string permission, string? resource, bool allowed, string reason, IReadOnlyCollection<string>? roles)
    {
        var ecs = new Dictionary<string, object?>
        {
            ["@timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
            ["ecs"] = new { version = EcsVersion },
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
                name = subjectName,
                // Omitted (WhenWritingNull) when the principal has no roles, e.g. a denied request.
                roles = roles is { Count: > 0 } ? roles : null
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

    // The audit event is the pre-rendered ECS JSON document, logged as a plain-string state so it is
    // exported over OTLP as the record body (see MessageActionAuditLog for the full rationale). Allow
    // and deny differ by level so sinks can alert on denies (Warning) without parsing the payload.
    static readonly EventId AllowEventId = new(1001);
    static readonly EventId DenyEventId = new(1002);
    static readonly Func<string, Exception?, string> IdentityFormatter = static (state, _) => state;
}
