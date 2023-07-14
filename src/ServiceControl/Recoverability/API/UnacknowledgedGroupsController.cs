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
        public UnacknowledgedGroupsController(IArchiveMessages archiver)
        {
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

            using (var session = store.OpenAsyncSession())
            {
                var retryHistory = await session.LoadAsync<RetryHistory>(RetryHistory.MakeId()).ConfigureAwait(false);
                if (retryHistory != null)
                {
                    if (retryHistory.Acknowledge(groupId, RetryType.FailureGroup))
                    {
                        await session.StoreAsync(retryHistory).ConfigureAwait(false);
                        await session.SaveChangesAsync().ConfigureAwait(false);

                        return Request.CreateResponse(HttpStatusCode.OK);
                    }
                }
            }

            return Request.CreateResponse(HttpStatusCode.NotFound);
        }

        readonly IArchiveMessages archiver;
    }
}