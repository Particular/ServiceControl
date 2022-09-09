namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Auditing.MessagesView;
    using ServiceControl.SagaAudit;
    using Monitoring;
    using Infrastructure;
    using Extensions;
    using NServiceBus.Logging;
    using NServiceBus.CustomChecks;
    using Auditing;
    using Indexes;
    using Raven.Client.Documents.Commands;
    using Transformers;

    class RavenDbAuditDataStore : IAuditDataStore
    {
        public RavenDbAuditDataStore(IDocumentStore store)
        {
            documentStore = store;
        }

        public async Task<QueryResult<SagaHistory>> QuerySagaHistoryById(Guid input)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var sagaHistory = await
                    session.Query<SagaHistory, SagaDetailsIndex>()
                        .Statistics(out var stats)
                        .SingleOrDefaultAsync(x => x.SagaId == input)
                        .ConfigureAwait(false);

                if (sagaHistory == null)
                {
                    return QueryResult<SagaHistory>.Empty();
                }

                return new QueryResult<SagaHistory>(sagaHistory, new QueryStatsInfo($"{stats.ResultEtag}", stats.TotalResults));
            }
        }

        public async Task<QueryResult<IList<MessagesView>>> GetMessages(bool includeSystemMessages, PagingInfo pagingInfo, SortInfo sortInfo)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .IncludeSystemMessagesWhere(includeSystemMessages)
                    .Sort(sortInfo)
                    .Paging(pagingInfo)
                    .ToMessagesView()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessages(string searchParam, PagingInfo pagingInfo, SortInfo sortInfo)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .Search(x => x.Query, searchParam)
                    .Sort(sortInfo)
                    .Paging(pagingInfo)
                    .ToMessagesView()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpointAndKeyword(SearchEndpointApi.Input input, PagingInfo pagingInfo, SortInfo sortInfo)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .Search(x => x.Query, input.Keyword)
                    .Where(m => m.ReceivingEndpointName == input.Endpoint)
                    .Sort(sortInfo)
                    .Paging(pagingInfo)
                    .ToMessagesView()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpoint(bool includeSystemMessages, string endpointName, PagingInfo pagingInfo, SortInfo sortInfo)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .IncludeSystemMessagesWhere(includeSystemMessages)
                    .Where(m => m.ReceivingEndpointName == endpointName)
                    .Sort(sortInfo)
                    .Paging(pagingInfo)
                    .ToMessagesView()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByConversationId(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .Where(m => m.ConversationId == conversationId)
                    .Sort(sortInfo)
                    .Paging(pagingInfo)
                    .ToMessagesView()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }

        public async Task<HttpResponseMessage> TryFetchFromIndex(HttpRequestMessage request, string messageId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var message = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .Select(msg =>
                        new
                        {
                            // TODO: Load these values properly
                            msg.MessageId,
                            Body = "",
                            //!string.IsNullOrEmpty(message.Body) ? message.Body : metadata["Body"],
                            BodySize = 0,
                            //(int)metadata["ContentLength"],
                            ContentType = "",
                            //metadata["ContentType"],
                            BodyNotStored = false
                            //(bool)metadata["BodyNotStored"]
                        }
                    )
                    .FirstOrDefaultAsync(f => f.MessageId == messageId)
                    .ConfigureAwait(false);

                if (message == null)
                {
                    return request.CreateResponse(HttpStatusCode.NotFound);
                }

                if (message.BodyNotStored && message.Body == null)
                {
                    return request.CreateResponse(HttpStatusCode.NoContent);
                }

                if (message.Body == null)
                {
                    return request.CreateResponse(HttpStatusCode.NotFound);
                }

                var response = request.CreateResponse(HttpStatusCode.OK);
                var content = new StringContent(message.Body);

                MediaTypeHeaderValue.TryParse(message.ContentType, out var parsedContentType);
                content.Headers.ContentType = parsedContentType ?? new MediaTypeHeaderValue("text/*");

                content.Headers.ContentLength = message.BodySize;
                response.Headers.ETag = new EntityTagHeaderValue($"\"{stats.ResultEtag}\"");
                response.Content = content;
                return response;
            }
        }

        public async Task<QueryResult<IList<KnownEndpointsView>>> QueryKnownEndpoints()
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var endpoints = await session.Advanced.LoadStartingWithAsync<KnownEndpoint>(KnownEndpoint.CollectionName, pageSize: 1024)
                    .ConfigureAwait(false);

                var knownEndpoints = endpoints
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

                return new QueryResult<IList<KnownEndpointsView>>(knownEndpoints, new QueryStatsInfo(string.Empty, knownEndpoints.Count));
            }
        }

        public Task Setup() => Task.CompletedTask;

        IDocumentStore documentStore;
    }
}