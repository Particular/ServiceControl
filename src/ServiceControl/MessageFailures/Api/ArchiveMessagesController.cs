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
    using Infrastructure.WebApi;
    using Recoverability;
    using ServiceControl.Persistence;
    using Microsoft.AspNet.SignalR;

    class ArchiveMessagesController : ApiController
    {
        public ArchiveMessagesController(IMessageSession session, IErrorMessageDataStore dataStore)
        {
            messageSession = session;
            this.dataStore = dataStore;
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
                var request = new ArchiveMessage { FailedMessageId = id };

                await messageSession.SendLocal(request).ConfigureAwait(false);
            }

            return Request.CreateResponse(HttpStatusCode.Accepted);
        }

        [Route("errors/groups/{classifier?}")]
        [HttpGet]
        public async Task<HttpResponseMessage> GetArchiveMessageGroups(string classifier = "Exception Type and Stack Trace")
        {
            var results = await dataStore.GetFailureGroupsByClassifier(classifier);

            return Negotiator.FromModel(Request, results)
                .WithDeterministicEtag(EtagHelper.CalculateEtag(results));
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

        [Route("archive/groups/id/{groupId}")]
        [HttpGet]
        public async Task<HttpResponseMessage> GetGroup(
            string groupId,
            [FromUri] string status,  // TODO: Previously Request.GetQueryStringValue<string>("status");
            [FromUri] string modified // TODO: Previously Request.GetQueryStringValue<string>("modified");
            )
        {
            var result = await dataStore.GetFailureGroupView(groupId, status, modified)
                .ConfigureAwait(false);

            return Negotiator
                .FromModel(Request, result.Results)
                .WithEtag(result.QueryStats.ETag);
        }

        readonly IErrorMessageDataStore dataStore;
        readonly IMessageSession messageSession;
    }
}