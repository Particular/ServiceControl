#nullable enable
namespace ServiceControl.Mcp;

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;
using NServiceBus;
using ServiceControl.Infrastructure.Auth;
using ServiceControl.Mcp.Authorization;
using ServiceControl.MessageFailures.InternalMessages;
using ServiceControl.Persistence;
using ServiceControl.Recoverability;

[Description(
    "Tools for retrying failed messages and failure groups.\n\n" +
    "Agent guidance:\n" +
    "1. Every tool in this group changes system state by sending failed messages back for reprocessing. Only retry after the underlying issue has been resolved.\n" +
    "2. Prefer RetryFailureGroup when all messages share the same root cause.\n" +
    "3. Retry a single failed message only when the user explicitly asks for that message.\n" +
    "4. All operations are asynchronous — they return Accepted or InProgress immediately and complete in the background.")]
public sealed class RetryTools(
    IMessageSession messageSession,
    RetryingManager retryingManager,
    TimeProvider timeProvider,
    ICurrentUserAccessor userAccessor,
    IHttpContextAccessor httpContextAccessor,
    IMessageActionAuditLog auditLog,
    McpAuthorizationService authorization)
{
    [McpServerTool(Name = "retry_failed_message", ReadOnly = false, Idempotent = false, Destructive = true, OpenWorld = false, UseStructuredContent = true), Description(
        "Use this tool to reprocess a single failed message by sending it back to its original queue. This operation changes system state. " +
        "Good for questions like: 'retry this message' or 'send this message back for processing'.")]
    public async Task<McpOperationResult> RetryFailedMessage(
        [Description("The failed message ID from a previous failed-message query result.")] string failedMessageId,
        CancellationToken cancellationToken = default)
    {
        await authorization.RequirePermissionAsync(McpPermissions.RetryFailure, cancellationToken);

        var user = ResolveUser();
        var operationId = GetOperationId();

        await auditLog.AuditedOperation(user, MessageActionKind.Retry, Permissions.ErrorMessagesRetry, MessageActionScope.Single,
            resource: failedMessageId, count: 1, operationId: operationId,
            ct => messageSession.Send<RetryMessage>(m => m.FailedMessageId = failedMessageId, AuditHeaders.LocalSendOptions(user, operationId), ct), cancellationToken);

        return McpOperationResult.Accepted($"Retry requested for message '{failedMessageId}'.");
    }

    [McpServerTool(Name = "retry_failure_group", ReadOnly = false, Idempotent = false, Destructive = true, OpenWorld = false, UseStructuredContent = true), Description(
        "Retry all failed messages in a failure group that share the same root cause. This operation changes system state. It may affect many messages. " +
        "Use the failure group ID from GetFailureGroups. Returns InProgress if a retry is already running for this group.")]
    public async Task<McpOperationResult> RetryFailureGroup(
        [Description("The failure group ID from previous GetFailureGroups results.")] string groupId,
        CancellationToken cancellationToken = default)
    {
        await authorization.RequirePermissionAsync(McpPermissions.RetryFailureGroup, cancellationToken);

        var started = timeProvider.GetUtcNow().UtcDateTime;
        if (retryingManager.IsOperationInProgressFor(groupId, RetryType.FailureGroup))
        {
            return McpOperationResult.InProgress($"A retry operation is already in progress for group '{groupId}'.");
        }

        var user = ResolveUser();
        var operationId = GetOperationId();

        await auditLog.AuditedOperation(user, MessageActionKind.Retry, Permissions.ErrorRecoverabilityGroupsRetry, MessageActionScope.Group,
            resource: groupId, count: null, operationId: operationId,
            async ct =>
            {
                await retryingManager.Wait(groupId, RetryType.FailureGroup, started, cancellationToken: ct);
                await messageSession.Send(new RetryAllInGroup
                {
                    GroupId = groupId,
                    Started = started
                }, AuditHeaders.LocalSendOptions(user, operationId), ct);
            }, cancellationToken);

        return McpOperationResult.Accepted($"Retry requested for all messages in failure group '{groupId}'.");
    }

    AuditUser ResolveUser() => userAccessor.Resolve(httpContextAccessor.HttpContext?.User);

    string GetOperationId()
    {
        var operationId = httpContextAccessor.HttpContext?.TraceIdentifier;
        return string.IsNullOrWhiteSpace(operationId) ? Guid.NewGuid().ToString("N") : operationId;
    }
}