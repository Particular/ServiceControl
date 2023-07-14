namespace ServiceControl.Recoverability.API
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using NServiceBus;
    using ServiceControl.Persistence.Recoverability;

    class FailureGroupsArchiveController : ApiController
    {
        public FailureGroupsArchiveController(IMessageSession bus, IArchiveMessages archiver)
        {
            this.bus = bus;
            this.archiver = archiver;
        }


        [Route("recoverability/groups/{groupId}/errors/archive")]
        [HttpPost]
        public async Task<HttpResponseMessage> ArchiveGroupErrors(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "missing groupId");
            }

            if (!archiver.IsOperationInProgressFor(groupId, ArchiveType.FailureGroup))
            {
                await archiver.StartArchiving(groupId, ArchiveType.FailureGroup)
                    .ConfigureAwait(false);

                await bus.SendLocal<ArchiveAllInGroup>(m => { m.GroupId = groupId; }).ConfigureAwait(false);
            }

            return Request.CreateResponse(HttpStatusCode.Accepted);
        }

        readonly IMessageSession bus;
        readonly IArchiveMessages archiver;
    }
}