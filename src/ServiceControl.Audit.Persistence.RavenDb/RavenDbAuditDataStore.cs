namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using Raven.Client;
    using ServiceControl.Audit.Auditing.MessagesView;
    using ServiceControl.SagaAudit;
    using Raven.Client.Linq;
    using ServiceControl.Audit.Monitoring;
    using ServiceControl.Audit.Infrastructure;
    using ServiceControl.Audit.Persistence.RavenDb.Extensions;
    using Raven.Abstractions.Data;
    using NServiceBus.Logging;
    using NServiceBus.CustomChecks;
    using ServiceControl.Audit.Auditing;

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

                return new QueryResult<SagaHistory>(sagaHistory, new QueryStatsInfo(stats.IndexEtag, stats.TotalResults));
            }
        }

        public async Task<QueryResult<IList<MessagesView>>> GetMessages(HttpRequestMessage request, PagingInfo pagingInfo)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .IncludeSystemMessagesWhere(request)
                    .Statistics(out var stats)
                    .Sort(request)
                    .Paging(pagingInfo)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessages(HttpRequestMessage request, string searchParam, PagingInfo pagingInfo)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .Search(x => x.Query, searchParam)
                    .Sort(request)
                    .Paging(pagingInfo)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpointAndKeyword(HttpRequestMessage request, SearchEndpointApi.Input input, PagingInfo pagingInfo)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .Search(x => x.Query, input.Keyword)
                    .Where(m => m.ReceivingEndpointName == input.Endpoint)
                    .Sort(request)
                    .Paging(pagingInfo)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpoint(HttpRequestMessage request, string endpointName, PagingInfo pagingInfo)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .IncludeSystemMessagesWhere(request)
                    .Where(m => m.ReceivingEndpointName == endpointName)
                    .Statistics(out var stats)
                    .Sort(request)
                    .Paging(pagingInfo)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByConversationId(HttpRequestMessage request, string conversationId, PagingInfo pagingInfo)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .Where(m => m.ConversationId == conversationId)
                    .Sort(request)
                    .Paging(pagingInfo)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
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
                    .TransformWith<MessagesBodyTransformer, MessagesBodyTransformer.Result>()
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
                response.Headers.ETag = new EntityTagHeaderValue($"\"{stats.IndexEtag}\"");
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

        public async Task MigrateEndpoints(int pageSize = 1024)
        {
            var knownEndpointsIndex = await documentStore.AsyncDatabaseCommands.GetIndexAsync("EndpointsIndex").ConfigureAwait(false);
            if (knownEndpointsIndex == null)
            {
                Logger.Debug("EndpointsIndex migration already completed.");
                // Index has already been deleted, no need to migrate
                return;
            }

            var dbStatistics = await documentStore.AsyncDatabaseCommands.GetStatisticsAsync().ConfigureAwait(false);
            var indexStats = dbStatistics.Indexes.First(index => index.Name == knownEndpointsIndex.Name);
            if (indexStats.Priority == IndexingPriority.Disabled)
            {
                Logger.Debug("EndpointsIndex already disabled. Deleting EndpointsIndex.");

                // This should only happen the second time the migration is attempted.
                // The index is disabled so the data should have been migrated. We can now delete the index.
                await documentStore.AsyncDatabaseCommands.DeleteIndexAsync(knownEndpointsIndex.Name).ConfigureAwait(false);
                return;
            }

            int previouslyDone = 0;
            do
            {
                using (var session = documentStore.OpenAsyncSession())
                {
                    var endpointsFromIndex = await session.Query<dynamic>(knownEndpointsIndex.Name, true)
                        .Skip(previouslyDone)
                        .Take(pageSize)
                        .ToListAsync()
                        .ConfigureAwait(false);

                    if (endpointsFromIndex.Count == 0)
                    {
                        Logger.Debug("No more records from EndpointsIndex to migrate.");
                        break;
                    }

                    previouslyDone += endpointsFromIndex.Count;

                    var knownEndpoints = endpointsFromIndex.Select(endpoint => new KnownEndpoint
                    {
                        Id = KnownEndpoint.MakeDocumentId(endpoint.Name, Guid.Parse(endpoint.HostId)),
                        Host = endpoint.Host,
                        HostId = Guid.Parse(endpoint.HostId),
                        Name = endpoint.Name,
                        LastSeen = DateTime.UtcNow // Set the imported date to be now since we have no better guess
                    });

                    using (var bulkInsert = documentStore.BulkInsert(options: new BulkInsertOptions
                    {
                        OverwriteExisting = true
                    }))
                    {
                        foreach (var endpoint in knownEndpoints)
                        {
                            bulkInsert.Store(endpoint);
                        }

                        Logger.Debug($"Migrating {endpointsFromIndex.Count} entries.");
                        await bulkInsert.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
            while (true);

            Logger.Debug("EndpointsIndex entries migrated. Disabling EndpointsIndex.");
            // Disable the index so it can be safely deleted in the next migration run
            await documentStore.AsyncDatabaseCommands.SetIndexPriorityAsync(knownEndpointsIndex.Name, IndexingPriority.Disabled).ConfigureAwait(false);
        }

        public async Task<CheckResult> PerformFailedAuditImportCheck(string errorMessage)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var query = session.Query<FailedAuditImport, FailedAuditImportIndex>();
                using (var ie = await session.Advanced.StreamAsync(query)
                    .ConfigureAwait(false))
                {
                    if (await ie.MoveNextAsync().ConfigureAwait(false))
                    {
                        Logger.Warn(errorMessage);
                        return CheckResult.Failed(errorMessage);
                    }
                }
            }

            return CheckResult.Pass;
        }

        public async Task SaveFailedAuditImport(FailedAuditImport message)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                await session.StoreAsync(message).ConfigureAwait(false);
                await session.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        public Task Setup() => Task.CompletedTask;

        IDocumentStore documentStore;

        static readonly ILog Logger = LogManager.GetLogger(typeof(RavenDbAuditDataStore));
    }
}