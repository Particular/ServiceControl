namespace ServiceControl.Audit.Infrastructure.SQL
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Auditing;
    using Auditing.MessagesView;
    using Dapper;
    using Monitoring;
    using ServiceControl.SagaAudit;

    class SqlQueryStore
    {
        readonly string connectionString;

        public SqlQueryStore(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task<(IList<MessagesView>, QueryStatsInfo)> GetAllMessages(HttpRequestMessage request)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                var (sortColumn, sortDirection) = GetSortOptions(request);
                var (pageNo, maxResultsPerPage) = GetPagingOptions(request);

                var query = SqlConstants.QueryMessagesView
                    .Replace("@SortColumn", sortColumn)
                    .Replace("@SortDirection", sortDirection);

                var results = await connection.QueryAsync(query, new
                {
                    Offset = (pageNo - 1) * maxResultsPerPage,
                    PageSize = maxResultsPerPage
                }).ConfigureAwait(false);

                var view = results.Select(o => new MessagesView
                {
                    MessageId = o.MessageId,
                    MessageType = o.MessageType,
                    IsSystemMessage = o.IsSystemMessage,
                    Status = o.IsRetried ? MessageStatus.ResolvedSuccessfully : MessageStatus.Successful,
                    TimeSent = o.TimeSent,
                    ProcessedAt = o.ProcessedAt,
                    ReceivingEndpoint = new EndpointDetails
                    {
                        Name = o.EndpointName,
                        Host = o.EndpointHost,
                        HostId = o.EndpointHostId
                    },
                    CriticalTime = o.CriticalTime != null ? TimeSpan.FromTicks(o.CriticalTime) : TimeSpan.Zero,
                    ProcessingTime = o.ProcessingTime != null ? TimeSpan.FromTicks(o.ProcessingTime) : TimeSpan.Zero,
                    DeliveryTime = o.DeliveryTime != null ? TimeSpan.FromTicks(o.DeliveryTime) : TimeSpan.Zero,
                    //Query = processedMessage.MessageMetadata.Select(_ => _.Value.ToString()).Union(new[] { string.Join(" ", message.Headers.Select(x => x.Value)) }).ToArray(),
                    ConversationId = o.ConversationId
                }).ToArray();

                return (view, new QueryStatsInfo());
            }

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

#pragma warning disable IDE0060 // Remove unused parameter
        public Task<IList<MessagesView>> MessagesByConversation(HttpRequestMessage request, out QueryStatsInfo stats)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            IList<MessagesView> result = new MessagesView[0].ToList();

            stats = QueryStatsInfo.Zero;

            return Task.FromResult(result);

            //throw new NotImplementedException();

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

        public async Task<(IList<KnownEndpointsView>, int)> GetKnownEndpoints()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                var result = await connection.QueryAsync<KnownEndpoint>(SqlConstants.QueryKnownEndpoints, new { PageSize = 1024 }).ConfigureAwait(false);

                var view = result.Select(ke => new KnownEndpointsView
                {
                    Id = DeterministicGuid.MakeId(ke.Name, ke.HostId.ToString()),
                    EndpointDetails = new EndpointDetails
                    {
                        Host = ke.Host,
                        HostId = ke.HostId,
                        Name = ke.Name
                    }
                }).ToArray();

                return (view, view.Length);
            }


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

        static T GetQueryStringValue<T>(HttpRequestMessage request, string key, T defaultValue = default)
        {
            Dictionary<string, string> queryStringDictionary;
            if (!request.Properties.TryGetValue("QueryStringAsDictionary", out var dictionaryAsObject))
            {
                queryStringDictionary = request.GetQueryNameValuePairs().ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
                request.Properties["QueryStringAsDictionary"] = queryStringDictionary;
            }
            else
            {
                queryStringDictionary = (Dictionary<string, string>)dictionaryAsObject;
            }

            queryStringDictionary.TryGetValue(key, out var value);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            return (T)Convert.ChangeType(value, typeof(T));
        }
        static (string, string) GetSortOptions(HttpRequestMessage request)
        {
            var sort = GetQueryStringValue(request, "sort", "time_sent");

            var columnNames = new Dictionary<string, string>()
            {
                {"processed_at", "ProcessedAt"},
                { "id", "MessageId" }, //TODO: figure out what is this id
                { "message_type", "MessageType" },
                { "time_sent", "TimeSent" },
                { "critical_time", "CriticalTime" },
                { "delivery_time", "DeliveryTime" },
                { "processing_time", "ProcessingTime" },
                { "status", "IsRetried" },
                { "message_id", "MessageId" }
            };

            var direction = GetQueryStringValue(request, "direction", "DESC");
            if (direction != "ASC" && direction != "DESC")
            {
                direction = "DESC";
            }

            return (columnNames[sort], direction);
        }

        static (int, int) GetPagingOptions(HttpRequestMessage request)
        {
            var maxResultsPerPage = GetQueryStringValue(request, "per_page", 50);
            if (maxResultsPerPage < 1)
            {
                maxResultsPerPage = 50;
            }

            var page = GetQueryStringValue(request, "page", 1);
            if (page < 1)
            {
                page = 1;
            }

            return (page, maxResultsPerPage);
        }
    }
}