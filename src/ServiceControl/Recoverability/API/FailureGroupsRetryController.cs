namespace ServiceControl.Recoverability.API
{
    using System;
    using System.Threading.Tasks;
    using Infrastructure.Auth;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;
    using ServiceControl.Persistence;

    [ApiController]
    [Route("api")]
    public class FailureGroupsRetryController(
        IMessageSession bus,
        RetryingManager retryingManager,
        ICurrentUserAccessor userAccessor,
        IMessageActionAuditLog auditLog) : ControllerBase
    {
        [Authorize(Policy = Permissions.ErrorRecoverabilityGroupsRetry)]
        [Route("recoverability/groups/{groupId:required:minlength(1)}/errors/retry")]
        [HttpPost]
        public async Task<IActionResult> ArchiveGroupErrors(string groupId)
        {
            var started = DateTime.UtcNow;

            if (!retryingManager.IsOperationInProgressFor(groupId, RetryType.FailureGroup))
            {
                var user = userAccessor.Resolve(User);
                var operationId = this.AuditOperationId();
                await auditLog.AuditedOperation(user, MessageActionKind.Retry,
                    Permissions.ErrorRecoverabilityGroupsRetry, MessageActionScope.Group,
                    resource: groupId, count: null, operationId: operationId, async () =>
                    {
                        await retryingManager.Wait(groupId, RetryType.FailureGroup, started);
                        await bus.Send(new RetryAllInGroup
                        {
                            GroupId = groupId,
                            Started = started
                        }, AuditHeaders.LocalSendOptions(user, operationId));
                    });
            }

            return Accepted();
        }
    }
}