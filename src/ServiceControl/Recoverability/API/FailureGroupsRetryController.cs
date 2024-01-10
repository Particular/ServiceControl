namespace ServiceControl.Recoverability.API
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;
    using ServiceControl.Persistence;

    [ApiController]
    [Route("api")]
    public class FailureGroupsRetryController : ControllerBase
    {
        public FailureGroupsRetryController(IMessageSession bus, RetryingManager retryingManager)
        {
            this.bus = bus;
            this.retryingManager = retryingManager;
        }


        [Route("recoverability/groups/{groupId}/errors/retry")]
        [HttpPost]
        public async Task<IActionResult> ArchiveGroupErrors(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                return BadRequest("missing groupId");
            }

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

        readonly IMessageSession bus;
        readonly RetryingManager retryingManager;
    }
}