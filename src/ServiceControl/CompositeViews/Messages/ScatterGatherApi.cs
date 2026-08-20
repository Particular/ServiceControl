namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using Persistence.Infrastructure;
    using ServiceBus.Management.Infrastructure.Settings;
    using JsonSerializer = System.Text.Json.JsonSerializer;

    interface IApi;

    // Non-generic, so statics live once rather than once per closed generic instantiation.
    public abstract class ScatterGatherApiBase
    {
        // Read raw, not via headers.ETag: an older instance sends an unquoted tag that
        // EntityTagHeaderValue cannot parse and drops without a word.
        internal static DataVersion ReadEtag(HttpResponseHeaders headers) =>
            headers.TryGetValues("ETag", out var values)
                ? DataVersion.FromClient(values.FirstOrDefault())
                : DataVersion.None;
    }

    public record ScatterGatherContext(PagingInfo PagingInfo);

    public abstract class ScatterGatherApi<TDataStore, TIn, TOut> : ScatterGatherApiBase, IApi
        where TIn : ScatterGatherContext
        where TOut : class
    {
        protected ScatterGatherApi(TDataStore store, Settings settings, IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor, ILogger logger)
        {
            DataStore = store;
            Settings = settings;
            HttpClientFactory = httpClientFactory;
            HttpContextAccessor = httpContextAccessor;
            this.logger = logger;
        }

        protected TDataStore DataStore { get; }

        Settings Settings { get; }
        IHttpClientFactory HttpClientFactory { get; }
        IHttpContextAccessor HttpContextAccessor { get; }

        public async Task<QueryResult<TOut>> Execute(TIn input, string pathAndQuery, CancellationToken cancellationToken = default)
        {
            var remotes = Settings.RemoteInstances;
            var instanceId = Settings.InstanceId;
            var authorizationHeader = HttpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();

            var tasks = new List<Task<QueryResult<TOut>>>(remotes.Length + 1)
            {
                LocalCall(input, instanceId, cancellationToken)
            };

            foreach (var remote in remotes)
            {
                if (remote.TemporarilyUnavailable)
                {
                    continue;
                }

                tasks.Add(RemoteCall(HttpClientFactory.CreateClient(remote.InstanceId), pathAndQuery, remote, authorizationHeader, cancellationToken));
            }

            var results = await Task.WhenAll(tasks);
            var response = AggregateResults(input, results);

            return response;
        }

        async Task<QueryResult<TOut>> LocalCall(TIn input, string instanceId, CancellationToken cancellationToken)
        {
            var result = await LocalQuery(input, cancellationToken);
            result.InstanceId = instanceId;
            result.IsLocalInstance = true;
            return result;
        }

        protected abstract Task<QueryResult<TOut>> LocalQuery(TIn input, CancellationToken cancellationToken = default);

        internal QueryResult<TOut> AggregateResults(TIn input, QueryResult<TOut>[] results)
        {
            var combinedResults = ProcessResults(input, results);

            return new QueryResult<TOut>(
                combinedResults,
                AggregateStats(input, results, combinedResults)
            );
        }

        protected abstract TOut ProcessResults(TIn input, QueryResult<TOut>[] results);

        protected virtual QueryStatsInfo AggregateStats(TIn input, IEnumerable<QueryResult<TOut>> results, TOut processedResults) =>
            Aggregate(results);

        /// <summary>
        /// For an API whose own instance holds none of the data. Its local result carries no version, and
        /// <see cref="DataVersion.Combine"/> reports <see cref="DataVersion.None"/> as soon as one result
        /// is missing one, which would leave every response with no ETag at all.
        /// </summary>
        protected static QueryStatsInfo AggregateStatsFromRemotesOnly(IEnumerable<QueryResult<TOut>> results) =>
            Aggregate(results.Where(result => !result.IsLocalInstance));

        static QueryStatsInfo Aggregate(IEnumerable<QueryResult<TOut>> results)
        {
            var reported = results.ToArray();

            if (reported.Length == 0)
            {
                return QueryStatsInfo.Zero;
            }

            var infos = reported.Select(x => x.QueryStats).ToArray();

            return new QueryStatsInfo(
                DataVersion.Combine(reported.Select(result => (result.InstanceId, result.QueryStats.Version))),
                infos.Sum(x => x.TotalCount),
                isStale: infos.Any(x => x.IsStale),
                infos.Max(x => x.HighestTotalCountOfAllTheInstances)
            );
        }

        async Task<QueryResult<TOut>> RemoteCall(HttpClient client, string pathAndQuery, RemoteInstanceSetting remoteInstanceSetting, string authorizationHeader, CancellationToken cancellationToken)
        {
            var fetched = await FetchAndParse(client, pathAndQuery, remoteInstanceSetting, authorizationHeader, cancellationToken);
            fetched.InstanceId = remoteInstanceSetting.InstanceId;
            return fetched;
        }

        async Task<QueryResult<TOut>> FetchAndParse(HttpClient httpClient, string pathAndQuery, RemoteInstanceSetting remoteInstanceSetting, string authorizationHeader, CancellationToken cancellationToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, pathAndQuery);
                var hasAuth = !string.IsNullOrEmpty(authorizationHeader);

                // Add Authorization header if present
                if (hasAuth)
                {
                    request.Headers.TryAddWithoutValidation("Authorization", authorizationHeader);
                }

                // Assuming SendAsync returns uncompressed response and the AutomaticDecompression is enabled on the http client.
                var rawResponse = await httpClient.SendAsync(request, cancellationToken);

                // special case - queried by conversation ID and nothing was found
                if (rawResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    return QueryResult<TOut>.Empty();
                }

                if (rawResponse.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    logger.LogWarning("Authentication failed when querying remote instance at {RemoteInstanceBaseAddress}. Ensure authentication is correctly configured.",
                        remoteInstanceSetting.BaseAddress);
                    return QueryResult<TOut>.Empty();
                }

                return await ParseResult(rawResponse, cancellationToken);
            }
            catch (HttpRequestException httpRequestException)
            {
                remoteInstanceSetting.TemporarilyUnavailable = true;
                logger.LogWarning(
                    httpRequestException,
                    "An HttpRequestException occurred when querying remote instance at {RemoteInstanceBaseAddress}. The instance will be temporarily disabled",
                    remoteInstanceSetting.BaseAddress);
                return QueryResult<TOut>.Empty();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The caller gave up on the whole scatter-gather, so this is not a per-remote timeout
                // to be absorbed into an empty result.
                throw;
            }
            catch (OperationCanceledException) // Intentional, used to gracefully handle timeout
            {
                logger.LogWarning("Failed to query remote instance at {RemoteInstanceBaseAddress} due to a timeout", remoteInstanceSetting.BaseAddress);
                return QueryResult<TOut>.Empty();
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to query remote instance at {RemoteInstanceBaseAddress}", remoteInstanceSetting.BaseAddress);
                return QueryResult<TOut>.Empty();
            }
        }

        static async Task<QueryResult<TOut>> ParseResult(HttpResponseMessage responseMessage, CancellationToken cancellationToken)
        {
            await using var responseStream = await responseMessage.Content.ReadAsStreamAsync(cancellationToken);
            var remoteResults = await JsonSerializer.DeserializeAsync<TOut>(responseStream, SerializerOptions.Default, cancellationToken);

            var totalCount = 0;
            if (responseMessage.Headers.TryGetValues("Total-Count", out var totalCounts))
            {
                totalCount = int.Parse(totalCounts.ElementAt(0));
            }

            var etag = ReadEtag(responseMessage.Headers);

            return new QueryResult<TOut>(remoteResults, QueryStatsInfo.Fresh(etag, totalCount));
        }

        readonly ILogger logger;
    }
}