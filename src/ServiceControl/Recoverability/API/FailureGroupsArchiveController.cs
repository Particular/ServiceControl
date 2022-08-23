namespace ServiceControl.Recoverability.API
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using NServiceBus;

    class FailureGroupsArchiveController : ApiController
    {
        public FailureGroupsArchiveController(IMessageSession bus, ArchivingManager archivingManager)
        {
            this.bus = bus;
            this.archivingManager = archivingManager;
        }


        [Route("recoverability/groups/{groupId}/errors/archive")]
        [HttpPost]
        public async Task<HttpResponseMessage> ArchiveGroupErrors(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "missing groupId");
            }

            if (!archivingManager.IsOperationInProgressFor(groupId, ArchiveType.FailureGroup))
            {
                await archivingManager.StartArchiving(groupId, ArchiveType.FailureGroup)
                    .ConfigureAwait(false);

                await bus.SendLocal<ArchiveAllInGroup>(m => { m.GroupId = groupId; }).ConfigureAwait(false);
            }

            return Request.CreateResponse(HttpStatusCode.Accepted);
        }

        readonly IMessageSession bus;
        readonly ArchivingManager archivingManager;
    }
}