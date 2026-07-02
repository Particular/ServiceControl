namespace ServiceControl.Recoverability.API
{
    using System;
    using System.Threading.Tasks;
    using Infrastructure.Auth;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;
    using ServiceControl.Persistence;

    [ApiController]
    [Route("api")]
    public class FailureGroupsRetryController(IMessageSession bus, RetryingManager retryingManager) : ControllerBase
    {
        [Authorize(Policy = Permissions.ErrorRecoverabilityGroupsRetry)]
        [Route("recoverability/groups/{groupId:required:minlength(1)}/errors/retry")]
        [HttpPost]
        public async Task<IActionResult> ArchiveGroupErrors(string groupId)
        {
            var started = DateTime.UtcNow;

            if (!retryingManager.IsOperationInProgressFor(groupId, RetryType.FailureGroup))
            {
                await retryingManager.Wait(groupId, RetryType.FailureGroup, started);

                await bus.SendLocal(new RetryAllInGroup
                {
                    GroupId = groupId,
                    Started = started
                });
            }

            return Accepted();
        }
    }
}