namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Infrastructure.WebApi;
    using Microsoft.Extensions.Logging;
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

    public record ScatterGatherContext(PagingInfo PagingInfo);

    public abstract class ScatterGatherApi<TDataStore, TIn, TOut> : ScatterGatherApiBase, IApi
        where TIn : ScatterGatherContext
        where TOut : class
    {
        protected ScatterGatherApi(TDataStore store, Settings settings, IHttpClientFactory httpClientFactory, ILogger logger)
        {
            DataStore = store;
            Settings = settings;
            HttpClientFactory = httpClientFactory;
            this.logger = logger;
        }

        protected TDataStore DataStore { get; }
        Settings Settings { get; }
        IHttpClientFactory HttpClientFactory { get; }

        public async Task<QueryResult<TOut>> Execute(TIn input, string pathAndQuery)
        {
            var remotes = Settings.RemoteInstances;
            var instanceId = Settings.InstanceId;
            var tasks = new List<Task<QueryResult<TOut>>>(remotes.Length + 1)
            {
                LocalCall(input, instanceId)
            };
            foreach (var remote in remotes)
            {
                if (remote.TemporarilyUnavailable)
                {
                    continue;
                }

                tasks.Add(RemoteCall(HttpClientFactory.CreateClient(remote.InstanceId), pathAndQuery, remote));
            }

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
                string.Concat(infos.OrderBy(x => x.ETag).Select(x => x.ETag)),
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
                logger.LogWarning(
                    httpRequestException,
                    "An HttpRequestException occurred when querying remote instance at {remoteInstanceSettingBaseAddress}. The instance at uri: {remoteInstanceSettingBaseAddress} will be temporarily disabled",
                    remoteInstanceSetting.BaseAddress,
                    remoteInstanceSetting.BaseAddress);
                return QueryResult<TOut>.Empty();
            }
            catch (OperationCanceledException) // Intentional, used to gracefully handle timeout
            {
                logger.LogWarning("Failed to query remote instance at {remoteInstanceSettingBaseAddress} due to a timeout", remoteInstanceSetting.BaseAddress);
                return QueryResult<TOut>.Empty();
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to query remote instance at {remoteInstanceSettingBaseAddress}", remoteInstanceSetting.BaseAddress);
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

        readonly ILogger logger;
    }
}