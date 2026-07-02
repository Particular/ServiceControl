#nullable enable
namespace ServiceControl.Infrastructure.Auth;

using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;

/// <summary>
/// Emits message-action audit entries as Elastic Common Schema (ECS) documents. Operation-level entries
/// go on <see cref="OperationCategory"/> (shared audit umbrella); per-message entries go on the
/// <see cref="MessageCategory"/> sub-category so operators can filter the high-volume per-message stream
/// independently through standard logging configuration.
/// </summary>
public sealed partial class MessageActionAuditLog : IMessageActionAuditLog
{
    public const string OperationCategory = AuthorizationAuditLog.AuditCategory;              // "ServiceControl.Audit"
    public const string MessageCategory = AuthorizationAuditLog.AuditCategory + ".Messages";  // "ServiceControl.Audit.Messages"

    // Relaxed escaping keeps the JSON readable for log sinks, matching AuthorizationAuditLog.
    static readonly JsonSerializerOptions EcsJsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

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

        var ecs = BuildEcsEvent(user, kind, permission, scope, resource, messageId: null, count, operationId, success);

        if (success)
        {
            LogOperation(operationLogger, ecs);
        }
        else
        {
            LogOperationFailure(operationLogger, ecs);
        }
    }

    public void MessageAction(AuditUser user, MessageActionKind kind, string permission, MessageActionScope scope, string messageId, string operationId, bool success = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(permission);
        ArgumentException.ThrowIfNullOrEmpty(messageId);
        ArgumentException.ThrowIfNullOrEmpty(operationId);

        var ecs = BuildEcsEvent(user, kind, permission, scope, resource: null, messageId, count: null, operationId, success);

        if (success)
        {
            LogMessage(messageLogger, ecs);
        }
        else
        {
            LogMessageFailure(messageLogger, ecs);
        }
    }

    static string BuildEcsEvent(AuditUser user, MessageActionKind kind, string permission, MessageActionScope scope, string? resource, string? messageId, int? count, string operationId, bool success)
    {
        var ecs = new Dictionary<string, object?>
        {
            ["@timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
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
                scope = scope.ToString().ToLowerInvariant(),
                resource,
                message = messageId is null ? null : new { id = messageId },
                count,
                operation = new { id = operationId }
            }
        };

        return JsonSerializer.Serialize(ecs, EcsJsonOptions);
    }

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "{AuditEvent}")]
    static partial void LogOperation(ILogger logger, string auditEvent);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Warning, Message = "{AuditEvent}")]
    static partial void LogOperationFailure(ILogger logger, string auditEvent);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "{AuditEvent}")]
    static partial void LogMessage(ILogger logger, string auditEvent);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning, Message = "{AuditEvent}")]
    static partial void LogMessageFailure(ILogger logger, string auditEvent);
}
