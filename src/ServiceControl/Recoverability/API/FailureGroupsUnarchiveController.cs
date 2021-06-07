namespace ServiceControl.Recoverability.API
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using NServiceBus;

    public class FailureGroupsUnarchiveController : ApiController
    {
        internal FailureGroupsUnarchiveController(IMessageSession bus, UnarchivingManager unarchivingManager)
        {
            this.bus = bus;
            this.unarchivingManager = unarchivingManager;
        }


        [Route("recoverability/groups/{groupId}/errors/unarchive")]
        [HttpPost]
        public async Task<HttpResponseMessage> UnarchiveGroupErrors(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "missing groupId");
            }

            if (!unarchivingManager.IsOperationInProgressFor(groupId, ArchiveType.FailureGroup))
            {
                await unarchivingManager.StartUnarchiving(groupId, ArchiveType.FailureGroup)
                    .ConfigureAwait(false);

                await bus.SendLocal<UnarchiveAllInGroup>(m => { m.GroupId = groupId; }).ConfigureAwait(false);
            }

            return Request.CreateResponse(HttpStatusCode.Accepted);
        }

        readonly IMessageSession bus;
        readonly UnarchivingManager unarchivingManager;
    }
}