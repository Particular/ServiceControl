namespace ServiceControl.Recoverability.API
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Raven.Client;

    class UnacknowledgedGroupsController : ApiController
    {
        public UnacknowledgedGroupsController(ArchivingManager archivingManager, IDocumentStore store)
        {
            this.archivingManager = archivingManager;
            this.store = store;
        }

        [Route("recoverability/unacknowledgedgroups/{groupId}")]
        [HttpDelete]
        public async Task<HttpResponseMessage> AcknowledgeOperation(string groupId)
        {
            if (archivingManager.IsArchiveInProgressFor(groupId))
            {
                archivingManager.DismissArchiveOperation(groupId, ArchiveType.FailureGroup);
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

        readonly ArchivingManager archivingManager;
        readonly IDocumentStore store;
    }
}