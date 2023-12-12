namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Infrastructure.Settings;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.AspNetCore.Mvc;
    using Newtonsoft.Json;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence.Infrastructure;

    interface IApi
    {
    }

    // used to hoist the static jsonSerializer field across the generic instances
    abstract class ScatterGatherApiBase
    {
        protected static JsonSerializer jsonSerializer = JsonSerializer.Create(JsonNetSerializerSettings.CreateDefault());
    }

    abstract class ScatterGatherApi<TDataStore, TIn, TOut> : ScatterGatherApiBase, IApi
        where TOut : class
    {
        protected ScatterGatherApi(TDataStore store, Settings settings, IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
        {
            this.httpContextAccessor = httpContextAccessor;
            DataStore = store;
            Settings = settings;
            HttpClientFactory = httpClientFactory;
            Logger = LogManager.GetLogger(GetType());
        }

        protected TDataStore DataStore { get; }
        Settings Settings { get; }
        IHttpClientFactory HttpClientFactory { get; }

        public async Task<TOut> Execute(ControllerBase controllerBase, TIn input)
        {
            var remotes = Settings.RemoteInstances;
            var pathAndQuery = controllerBase.Request.GetEncodedPathAndQuery();
            var instanceId = InstanceIdGenerator.FromApiUrl(Settings.ApiUrl);
            var tasks = new List<Task<QueryResult<TOut>>>(remotes.Length + 1)
            {
                LocalCall(controllerBase.Request, input, instanceId)
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
            var response = AggregateResults(results);

            httpContextAccessor.HttpContext.Response.WithQueryResults(response.QueryStats, pagingInfo);

            return response.Results;
        }

        async Task<QueryResult<TOut>> LocalCall(PagingInfo pagingInfo, TIn input, string instanceId)
        {
            var result = await LocalQuery(pagingInfo, input);
            result.InstanceId = instanceId;
            return result;
        }

        protected abstract Task<QueryResult<TOut>> LocalQuery(PagingInfo pagingInfo, TIn input);

        internal QueryResult<TOut> AggregateResults(QueryResult<TOut>[] results)
        {
            var combinedResults = ProcessResults(results);

            return new QueryResult<TOut>(
                combinedResults,
                AggregateStats(results, combinedResults)
            );
        }

        protected abstract TOut ProcessResults(QueryResult<TOut>[] results);

        protected virtual QueryStatsInfo AggregateStats(IEnumerable<QueryResult<TOut>> results, TOut processedResults)
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
                Logger.Warn($"An HttpRequestException occurred when querying remote instance at {remoteInstanceSetting.ApiUri}. The instance at uri: {remoteInstanceSetting.ApiUri} will be temporarily disabled.",
                    httpRequestException);
                return QueryResult<TOut>.Empty();
            }
            catch (OperationCanceledException)
            {
                Logger.Warn($"Failed to query remote instance at {remoteInstanceSetting.ApiUri} due to a timeout");
                return QueryResult<TOut>.Empty();
            }
            catch (Exception exception)
            {
                Logger.Warn($"Failed to query remote instance at {remoteInstanceSetting.ApiUri}.", exception);
                return QueryResult<TOut>.Empty();
            }
        }

        static async Task<QueryResult<TOut>> ParseResult(HttpResponseMessage responseMessage)
        {
            using (var responseStream = await responseMessage.Content.ReadAsStreamAsync())
            using (var jsonReader = new JsonTextReader(new StreamReader(responseStream)))
            {
                var remoteResults = jsonSerializer.Deserialize<TOut>(jsonReader);

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
        }

        readonly ILog Logger;
        readonly IHttpContextAccessor httpContextAccessor;
    }
}