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

            var user = userAccessor.Resolve(User);
            var operationId = this.AuditOperationId();
            auditLog.Operation(user, MessageActionKind.Retry,
                Permissions.ErrorRecoverabilityGroupsRetry, MessageActionScope.Group,
                resource: groupId, count: null, operationId: operationId);

            if (!retryingManager.IsOperationInProgressFor(groupId, RetryType.FailureGroup))
            {
                await retryingManager.Wait(groupId, RetryType.FailureGroup, started);

                var sendOptions = new SendOptions();
                sendOptions.RouteToThisEndpoint();
                AuditHeaders.Stamp(sendOptions, user, operationId);

                await bus.Send(new RetryAllInGroup
                {
                    GroupId = groupId,
                    Started = started
                }, sendOptions);
            }

            return Accepted();
        }
    }
}