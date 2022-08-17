namespace ServiceControl.Audit.Persistence.InMemory
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Auditing.MessagesView;
    using ServiceControl.SagaAudit;
    using ServiceControl.Audit.Monitoring;
    using ServiceControl.Audit.Infrastructure;

    //TODO - not all done as not sure on some of the object structure
    class InMemoryAuditDataStore : IAuditDataStore
    {
        List<SagaHistory> sagaHistories;
        List<MessagesView> messageViews;
        List<KnownEndpoint> knownEndpoints;

        public InMemoryAuditDataStore()
        {
            sagaHistories = new List<SagaHistory>();
            messageViews = new List<MessagesView>();
            knownEndpoints = new List<KnownEndpoint>();
        }

        public async Task<QueryResult<SagaHistory>> QuerySagaHistoryById(HttpRequestMessage request, Guid input)
        {
            var sagaHistory = sagaHistories.FirstOrDefault(w => w.SagaId == input);

            if (sagaHistory == null)
            {
                return await Task.FromResult(QueryResult<SagaHistory>.Empty()).ConfigureAwait(false);
            }

            return await Task.FromResult(new QueryResult<SagaHistory>(sagaHistory, new QueryStatsInfo(string.Empty, 1))).ConfigureAwait(false);
        }

        public async Task<QueryResult<IList<MessagesView>>> GetMessages(HttpRequestMessage request)
        {
            return await Task.FromResult(new QueryResult<IList<MessagesView>>(messageViews, new QueryStatsInfo(string.Empty, messageViews.Count))).ConfigureAwait(false);
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessages(HttpRequestMessage request, string searchParam)
        {
            //TODO how should searchParam be used in this query?
            return await Task.FromResult(new QueryResult<IList<MessagesView>>(messageViews, new QueryStatsInfo(string.Empty, messageViews.Count))).ConfigureAwait(false);
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpointAndKeyword(HttpRequestMessage request, SearchEndpointApi.Input input)
        {
            //TODO how should input.Keyword be used in this query?
            var matched = messageViews.Where(w => w.ReceivingEndpoint.Name == input.Endpoint).ToList();
            return await Task.FromResult(new QueryResult<IList<MessagesView>>(matched, new QueryStatsInfo(string.Empty, matched.Count))).ConfigureAwait(false);
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpoint(HttpRequestMessage request, string endpointName)
        {
            var matched = messageViews.Where(w => w.ReceivingEndpoint.Name == endpointName).ToList();
            return await Task.FromResult(new QueryResult<IList<MessagesView>>(matched, new QueryStatsInfo(string.Empty, matched.Count))).ConfigureAwait(false);
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByConversationId(HttpRequestMessage request, string conversationId)
        {
            var matched = messageViews.Where(w => w.ConversationId == conversationId).ToList();
            return await Task.FromResult(new QueryResult<IList<MessagesView>>(matched, new QueryStatsInfo(string.Empty, matched.Count))).ConfigureAwait(false);
        }

        public async Task<HttpResponseMessage> TryFetchFromIndex(HttpRequestMessage request, string messageId)
        {
            var message = messageViews.FirstOrDefault(w => w.MessageId == messageId);

            if (message == null)
            {
                return request.CreateResponse(HttpStatusCode.NotFound);
            }

            //TODO not sure where these are coming from - can see it's from the 
            //if (message.BodyNotStored && message.Body == null)
            //{
            //    return request.CreateResponse(HttpStatusCode.NoContent);
            //}

            //if (message.Body == null)
            //{
            //    return request.CreateResponse(HttpStatusCode.NotFound);
            //}

            var response = request.CreateResponse(HttpStatusCode.OK);
            //var content = new StringContent(message.Body);

            //MediaTypeHeaderValue.TryParse(message.ContentType, out var parsedContentType);
            //content.Headers.ContentType = parsedContentType ?? new MediaTypeHeaderValue("text/*");

            //content.Headers.ContentLength = message.BodySize;
            //response.Headers.ETag = new EntityTagHeaderValue($"\"{string.Empty}\"");
            //response.Content = content;
            return await Task.FromResult(response).ConfigureAwait(false);
        }

        public async Task<QueryResult<IList<KnownEndpointsView>>> QueryKnownEndpoints(HttpRequestMessage request)
        {
            var knownEndpointsView = knownEndpoints
                .Select(x => new KnownEndpointsView
                {
                    Id = DeterministicGuid.MakeId(x.Name, x.HostId.ToString()),
                    EndpointDetails = new EndpointDetails
                    {
                        Host = x.Host,
                        HostId = x.HostId,
                        Name = x.Name
                    },
                    HostDisplayName = x.Host
                })
                .ToList();

            return await Task.FromResult(new QueryResult<IList<KnownEndpointsView>>(knownEndpointsView, new QueryStatsInfo(string.Empty, knownEndpointsView.Count))).ConfigureAwait(false);
        }

        public Task Setup() => Task.CompletedTask;
    }
}