namespace ServiceControl.Recoverability.API
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using NServiceBus;
    using ServiceControl.Persistence;

    class FailureGroupsRetryController : ApiController
    {
        public FailureGroupsRetryController(IMessageSession bus, RetryingManager retryingManager)
        {
            this.bus = bus;
            this.retryingManager = retryingManager;
        }


        [Route("recoverability/groups/{groupId}/errors/retry")]
        [HttpPost]
        public async Task<HttpResponseMessage> ArchiveGroupErrors(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "missing groupId");
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

            return Request.CreateResponse(HttpStatusCode.Accepted);
        }

        readonly IMessageSession bus;
        readonly RetryingManager retryingManager;
    }
}