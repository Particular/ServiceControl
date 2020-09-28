namespace ServiceControl.CustomChecks
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using System.Web.Http.Results;
    using Infrastructure.Extensions;
    using Infrastructure.WebApi;
    using NServiceBus;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Linq;

    public class CustomCheckController : ApiController
    {
        internal CustomCheckController(IDocumentStore documentStore, IMessageSession messageSession)
        {
            this.messageSession = messageSession;
            this.documentStore = documentStore;
        }

        [Route("customchecks")]
        [HttpGet]
        public async Task<HttpResponseMessage> CustomChecks(string status = null)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var query =
                    session.Query<CustomCheck, CustomChecksIndex>().Statistics(out var stats);

                query = AddStatusFilter(query, status);

                var results = await query
                    .Paging(Request)
                    .ToListAsync()
                    .ConfigureAwait(false);

                return Negotiator
                    .FromModel(Request, results)
                    .WithPagingLinksAndTotalCount(stats.TotalResults, Request)
                    .WithEtag(stats);
            }
        }

        [Route("customchecks/{id}")]
        [HttpDelete]
        public async Task<StatusCodeResult> Delete(Guid id)
        {
            await messageSession.SendLocal(new DeleteCustomCheck {Id = id}).ConfigureAwait(false);

            return StatusCode(HttpStatusCode.Accepted);
        }

        static IRavenQueryable<CustomCheck> AddStatusFilter(IRavenQueryable<CustomCheck> query, string status)
        {
            if (status == null)
            {
                return query;
            }

            if (status == "fail")
            {
                query = query.Where(c => c.Status == Status.Fail);
            }

            if (status == "pass")
            {
                query = query.Where(c => c.Status == Status.Pass);
            }

            return query;
        }

        IDocumentStore documentStore;
        IMessageSession messageSession;
    }
}