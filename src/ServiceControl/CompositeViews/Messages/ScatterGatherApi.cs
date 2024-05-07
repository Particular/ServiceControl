namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Infrastructure.WebApi;
    using NServiceBus.Logging;
    using Persistence.Infrastructure;
    using ServiceBus.Management.Infrastructure.Settings;
    using JsonSerializer = System.Text.Json.JsonSerializer;

    interface IApi
    {
    }

    // used to hoist the static jsonSerializer field across the generic instances
    public abstract class ScatterGatherApiBase
    {
    }

    public abstract class ScatterGatherApi<TIn, TOut> : ScatterGatherApiBase, IApi
        where TIn : ScatterGatherContext
        where TOut : class
    {
        protected ScatterGatherApi(Settings settings, IHttpClientFactory httpClientFactory)
        {
            Settings = settings;
            HttpClientFactory = httpClientFactory;
            logger = LogManager.GetLogger(GetType());
        }
        protected Settings Settings { get; }
        protected IHttpClientFactory HttpClientFactory { get; }

        protected void ExecuteRemotes(List<Task<QueryResult<TOut>>> tasks, string pathAndQuery)
        {
            var remotes = Settings.RemoteInstances;
            foreach (var remote in remotes)
            {
                if (remote.TemporarilyUnavailable)
                {
                    continue;
                }

                tasks.Add(RemoteCall(HttpClientFactory.CreateClient(remote.InstanceId), pathAndQuery, remote));
            }
        }
        public virtual async Task<QueryResult<TOut>> Execute(TIn input, string pathAndQuery)
        {
            var tasks = new List<Task<QueryResult<TOut>>>(Settings.RemoteInstances.Length);
            ExecuteRemotes(tasks, pathAndQuery);
            var results = await Task.WhenAll(tasks);
            var response = AggregateResults(input, results);
            return response;
        }

        internal QueryResult<TOut> AggregateResults(TIn input, QueryResult<TOut>[] results)
        {
            var combinedResults = ProcessResults(input, results);

            return new QueryResult<TOut>(
                combinedResults,
                AggregateStats(input, results, combinedResults)
            );
        }

        protected abstract TOut ProcessResults(TIn input, QueryResult<TOut>[] results);

        protected virtual QueryStatsInfo AggregateStats(TIn input, IEnumerable<QueryResult<TOut>> results, TOut processedResults)
        {
            var infos = results.Select(x => x.QueryStats).ToArray();

            return new QueryStatsInfo(
                string.Join("", infos.OrderBy(x => x.ETag).Select(x => x.ETag)),
                infos.Sum(x => x.TotalCount),
                isStale: infos.Any(x => x.IsStale),
                infos.Max(x => x.HighestTotalCountOfAllTheInstances)
            );
        }

        async Task<QueryResult<TOut>> RemoteCall(HttpClient client, string pathAndQuery, RemoteInstanceSetting remoteInstanceSetting)
        {
            var fetched = await FetchAndParse(client, pathAndQuery, remoteInstanceSetting);
            fetched.InstanceId = remoteInstanceSetting.InstanceId;
            return fetched;
        }

        async Task<QueryResult<TOut>> FetchAndParse(HttpClient httpClient, string pathAndQuery, RemoteInstanceSetting remoteInstanceSetting)
        {
            try
            {
                // Assuming SendAsync returns uncompressed response and the AutomaticDecompression is enabled on the http client.
                var rawResponse = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, pathAndQuery));
                // special case - queried by conversation ID and nothing was found
                if (rawResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    return QueryResult<TOut>.Empty();
                }

                return await ParseResult(rawResponse);
            }
            catch (HttpRequestException httpRequestException)
            {
                remoteInstanceSetting.TemporarilyUnavailable = true;
                logger.Warn(
                    $"An HttpRequestException occurred when querying remote instance at {remoteInstanceSetting.BaseAddress}. The instance at uri: {remoteInstanceSetting.BaseAddress} will be temporarily disabled.",
                    httpRequestException);
                return QueryResult<TOut>.Empty();
            }
            catch (OperationCanceledException)
            {
                logger.Warn($"Failed to query remote instance at {remoteInstanceSetting.BaseAddress} due to a timeout");
                return QueryResult<TOut>.Empty();
            }
            catch (Exception exception)
            {
                logger.Warn($"Failed to query remote instance at {remoteInstanceSetting.BaseAddress}.", exception);
                return QueryResult<TOut>.Empty();
            }
        }

        static async Task<QueryResult<TOut>> ParseResult(HttpResponseMessage responseMessage)
        {
            await using var responseStream = await responseMessage.Content.ReadAsStreamAsync();
            var remoteResults = await JsonSerializer.DeserializeAsync<TOut>(responseStream, SerializerOptions.Default);

            var totalCount = 0;
            if (responseMessage.Headers.TryGetValues("Total-Count", out var totalCounts))
            {
                totalCount = int.Parse(totalCounts.ElementAt(0));
            }

            string etag = responseMessage.Headers.ETag?.Tag;
            if (etag != null)
            {
                // Strip quotes from Etag, checking for " which isn't really needed as Etag always has quotes but not 100% certain.
                // Later the value is joined into a new Etag when the results are aggregated and returned
                if (etag.StartsWith("\""))
                {
                    etag = etag.Substring(1, etag.Length - 2);
                }
            }

            return new QueryResult<TOut>(remoteResults, new QueryStatsInfo(etag, totalCount, isStale: false));
        }

        readonly ILog logger;
    }

    public record ScatterGatherContext(PagingInfo PagingInfo);

    public abstract class ScatterGatherApi<TDataStore, TIn, TOut> : ScatterGatherApi<TIn, TOut>, IApi
        where TIn : ScatterGatherContext
        where TOut : class
    {
        protected ScatterGatherApi(TDataStore store, Settings settings, IHttpClientFactory httpClientFactory)
            : base(settings, httpClientFactory)
        {
            DataStore = store;
        }

        protected TDataStore DataStore { get; }

        public override async Task<QueryResult<TOut>> Execute(TIn input, string pathAndQuery)
        {
            var tasks = new List<Task<QueryResult<TOut>>>(Settings.RemoteInstances.Length + 1)
            {
                LocalCall(input, Settings.InstanceId)
            };
            ExecuteRemotes(tasks, pathAndQuery);

            var results = await Task.WhenAll(tasks);
            var response = AggregateResults(input, results);

            return response;
        }

        async Task<QueryResult<TOut>> LocalCall(TIn input, string instanceId)
        {
            var result = await LocalQuery(input);
            result.InstanceId = instanceId;
            return result;
        }

        protected abstract Task<QueryResult<TOut>> LocalQuery(TIn input);
    }
}