namespace ServiceControl.Audit.Infrastructure.SQL
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Auditing;
    using Auditing.MessagesView;
    using Monitoring;
    using ServiceControl.SagaAudit;

    class SqlQueryStore
    {
#pragma warning disable IDE0052 // Remove unread private members
        readonly string connectionString;
#pragma warning restore IDE0052 // Remove unread private members

        public SqlQueryStore(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public Task<IList<MessagesView>> GetAllMessages(HttpRequestMessage request, out QueryStatsInfo stats)
        {
            throw new NotImplementedException();

            /*
             using (var session = Store.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .IncludeSystemMessagesWhere(request)
                    .Statistics(out var stats)
                    .Sort(request)
                    .Paging(request)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

            }
             */
        }

        public Task<IList<MessagesView>> GetAllMessagesForEndpoint(HttpRequestMessage reqeust, out QueryStatsInfo stats)
        {
            throw new NotImplementedException();

            /*
            using (var session = Store.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .IncludeSystemMessagesWhere(request)
                    .Where(m => m.ReceivingEndpointName == input)
                    .Statistics(out var stats)
                    .Sort(request)
                    .Paging(request)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

            }
            */
        }

        public Task<IList<MessagesView>> MessagesByConversation(HttpRequestMessage request, out QueryStatsInfo stats)
        {
            throw new NotImplementedException();

            /*
            using (var session = Store.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .Where(m => m.ConversationId == conversationId)
                    .Sort(request)
                    .Paging(request)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

            }
            */
        }

        public Task<IList<MessagesView>> FullTextSearch(HttpRequestMessage request, out QueryStatsInfo stats)
        {
            throw new NotImplementedException();

            /*
            using (var session = Store.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .Search(x => x.Query, input)
                    .Sort(request)
                    .Paging(request)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

            }
            */
        }

        public Task<IList<MessagesView>> SearchEndpoint(HttpRequestMessage request, SearchEndpointApi.Input input, out QueryStatsInfo queryStatsInfo)
        {
            throw new NotImplementedException();

            /*
            using (var session = Store.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .Search(x => x.Query, input.Keyword)
                    .Where(m => m.ReceivingEndpointName == input.Endpoint)
                    .Sort(request)
                    .Paging(request)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

            }
            */
        }

        public Task<SagaHistory> GetSagaById(Guid input, out string etag, out int totalResults)
        {
            throw new NotImplementedException();

            /*
            using (var session = Store.OpenAsyncSession())
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

            }
            */
        }

        public Task<IList<KnownEndpointsView>> GetKnownEndpoints(out int totalCount)
        {
            throw new NotImplementedException();

            /*
            using (var session = Store.OpenAsyncSession())
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

            }
            */
        }

        public Task<bool> FailedAuditImportsExist()
        {
            return Task.FromResult(false);
            //throw new NotImplementedException();

            /*
            using (var session = store.OpenAsyncSession())
            {
                var query = session.Query<FailedAuditImport, FailedAuditImportIndex>();
                using (var ie = await session.Advanced.StreamAsync(query)
                           .ConfigureAwait(false))
                {
                    if (await ie.MoveNextAsync().ConfigureAwait(false))
                    {
                        Logger.Warn(message);
                        return CheckResult.Failed(message);
                    }
                }
            }

            return CheckResult.Pass;
            */
        }

        public Task<IEnumerable<FailedAuditImport>> GetFailedAuditImports()
        {
            throw new NotImplementedException();
        }

        public Task Initialize()
        {
            return Task.CompletedTask;
        }
    }
}