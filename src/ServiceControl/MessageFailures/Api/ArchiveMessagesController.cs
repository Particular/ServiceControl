namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using InternalMessages;
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.Infrastructure.WebApi;
    using ServiceControl.Recoverability;

    public class ArchiveMessagesController : ApiController
    {
        public ArchiveMessagesController(IMessageSession session, IDocumentStore store)
        {
            messageSession = session;
            this.store = store;
        }

        [Route("errors/archive")]
        [HttpPost]
        [HttpPatch]
        public async Task<HttpResponseMessage> ArchiveBatch(List<string> messageIds)
        {
            if (messageIds.Any(string.IsNullOrEmpty))
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            foreach (var id in messageIds)
            {
                var request = new ArchiveMessage {FailedMessageId = id};

                await messageSession.SendLocal(request).ConfigureAwait(false);
            }

            return Request.CreateResponse(HttpStatusCode.Accepted);
        }

        [Route("errors/groups/{classifier?}")]
        [HttpGet]
        public async Task<HttpResponseMessage> GetArchiveMessageGroups([FromUri] string classifierFilter = null, string classifier = "Exception Type and Stack Trace")
        {
            using (var session = store.OpenAsyncSession())
            {
                var groups = (IQueryable<FailureGroupView>)session.Query<FailureGroupView, ArchivedGroupsViewIndex>();
                if (classifier != null)
                {
                    groups = groups.Where(v => v.Type == classifier);
                }

                var results = await groups.OrderByDescending(x => x.Last)
                    .Take(200)
                    .ToListAsync()
                    .ConfigureAwait(false);

                return Negotiator.FromModel(Request, results)
                    .WithDeterministicEtag(EtagHelper.CalculateEtag(results));
            }
        }

        [Route("errors/{messageid}/archive")]
        [HttpPost]
        [HttpPatch]
        public async Task<HttpResponseMessage> Archive(string messageId)
        {
            if (string.IsNullOrEmpty(messageId))
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            await messageSession.SendLocal<ArchiveMessage>(m => { m.FailedMessageId = messageId; }).ConfigureAwait(false);

            return Request.CreateResponse(HttpStatusCode.Accepted);
        }

        readonly IDocumentStore store;
        readonly IMessageSession messageSession;
    }
}