namespace ServiceControl.Recoverability.API
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Recoverability;

    class UnacknowledgedGroupsController : ApiController
    {
        public UnacknowledgedGroupsController(IRetryHistoryDataStore retryStore, IArchiveMessages archiver)
        {
            this.retryStore = retryStore;
            this.archiver = archiver;
        }

        [Route("recoverability/unacknowledgedgroups/{groupId}")]
        [HttpDelete]
        public async Task<HttpResponseMessage> AcknowledgeOperation(string groupId)
        {
            if (archiver.IsArchiveInProgressFor(groupId))
            {
                archiver.DismissArchiveOperation(groupId, ArchiveType.FailureGroup);
                return Request.CreateResponse(HttpStatusCode.OK);
            }

            var success = await retryStore.AcknowledgeRetryGroup(groupId).ConfigureAwait(false);

            if (success)
            {
                return Request.CreateResponse(HttpStatusCode.OK);
            }


            return Request.CreateResponse(HttpStatusCode.NotFound);
        }

        readonly IRetryHistoryDataStore retryStore;
        readonly IArchiveMessages archiver;
    }
}