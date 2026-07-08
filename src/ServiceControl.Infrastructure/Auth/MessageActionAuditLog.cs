#nullable enable
namespace ServiceControl.Infrastructure.Auth;

using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;

/// <summary>
/// Emits message-action audit entries as Elastic Common Schema (ECS) documents. Operation-level entries
/// go on <see cref="OperationCategory"/> (shared audit umbrella); per-message entries go on the
/// <see cref="MessageCategory"/> sub-category so operators can filter the high-volume per-message stream
/// independently through standard logging configuration.
/// </summary>
public sealed class MessageActionAuditLog : IMessageActionAuditLog
{
    public const string OperationCategory = AuthorizationAuditLog.AuditCategory;              // "ServiceControl.Audit"
    public const string MessageCategory = AuthorizationAuditLog.AuditCategory + ".Messages";  // "ServiceControl.Audit.Messages"

    readonly ILogger operationLogger;
    readonly ILogger messageLogger;

    public MessageActionAuditLog(ILoggerFactory loggerFactory)
    {
        operationLogger = loggerFactory.CreateLogger(OperationCategory);
        messageLogger = loggerFactory.CreateLogger(MessageCategory);
    }

    public void Operation(AuditUser user, MessageActionKind kind, string permission, MessageActionScope scope, string? resource, int? count, string operationId, bool success = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(permission);
        ArgumentException.ThrowIfNullOrEmpty(operationId);

        // Checked before building the ECS document — not worth building for a filtered-out category.
        var level = success ? LogLevel.Information : LogLevel.Warning;
        if (!operationLogger.IsEnabled(level))
        {
            return;
        }

        var ecs = BuildEcsEvent(user, kind, permission, scope, resource, messageId: null, count, operationId, success);
        operationLogger.Log(level, OperationEventId, ecs, null, IdentityFormatter);
    }

    public void MessageAction(AuditUser user, MessageActionKind kind, string permission, MessageActionScope scope, string messageId, string operationId, bool success = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(permission);
        ArgumentException.ThrowIfNullOrEmpty(messageId);
        ArgumentException.ThrowIfNullOrEmpty(operationId);

        // Bulk operations emit one entry per message on hot paths (retry staging, archive batches),
        // and operators are told they can filter this category — skip the document build entirely
        // when the entry would be dropped.
        var level = success ? LogLevel.Information : LogLevel.Warning;
        if (!messageLogger.IsEnabled(level))
        {
            return;
        }

        var ecs = BuildEcsEvent(user, kind, permission, scope, resource: null, messageId, count: null, operationId, success);
        messageLogger.Log(level, MessageEventId, ecs, null, IdentityFormatter);
    }

    static string BuildEcsEvent(AuditUser user, MessageActionKind kind, string permission, MessageActionScope scope, string? resource, string? messageId, int? count, string operationId, bool success)
    {
        var ecs = new Dictionary<string, object?>
        {
            ["@timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
            ["ecs"] = new { version = AuthorizationAuditLog.EcsVersion },
            ["event"] = new
            {
                kind = "event",
                category = new[] { "configuration" },
                type = new[] { kind == MessageActionKind.Archive ? "deletion" : "change" },
                action = permission,
                outcome = success ? "success" : "failure"
            },
            ["user"] = new
            {
                id = user.Id,
                name = user.Name
            },
            ["servicecontrol"] = new
            {
                permission,
                scope = ScopeName(scope),
                resource,
                message = messageId is null ? null : new { id = messageId },
                count,
                operation = new { id = operationId }
            }
        };

        return JsonSerializer.Serialize(ecs, AuthorizationAuditLog.EcsJsonOptions);
    }

    // Constant lowercase names instead of ToString().ToLowerInvariant(): per-message entries call this
    // once per message in bulk loops, and the two throwaway strings per entry add up.
    static string ScopeName(MessageActionScope scope) => scope switch
    {
        MessageActionScope.Single => "single",
        MessageActionScope.Batch => "batch",
        MessageActionScope.Group => "group",
        MessageActionScope.Queue => "queue",
        MessageActionScope.Endpoint => "endpoint",
        MessageActionScope.All => "all",
        MessageActionScope.Range => "range",
        _ => scope.ToString().ToLowerInvariant()
    };

    // Logged with the pre-rendered document as the state, not as a "{AuditEvent}" template parameter:
    // a parameterized message is exported over OTLP with the literal "{AuditEvent}" placeholder as the
    // record body and the JSON only in an attribute — backends that map body → message show the
    // placeholder. A plain-string state exports the document exactly once, as the record body; NLog is
    // unaffected either way and writes the same line to audit.json.
    static readonly EventId OperationEventId = new(2001);
    static readonly EventId MessageEventId = new(2002);
    static readonly Func<string, Exception?, string> IdentityFormatter = static (state, _) => state;
}
