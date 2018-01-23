namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Nancy;
    using Newtonsoft.Json;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using HttpStatusCode = System.Net.HttpStatusCode;

    public static class MessageViewQueryExtensions
    {
        static JsonSerializer jsonSerializer = JsonSerializer.Create(JsonNetSerializer.CreateDefault());
        static ILog logger = LogManager.GetLogger(typeof(MessageViewQueryExtensions));

        private static QueryResult EmptyResult = new QueryResult(new List<MessagesView>(), new QueryStatsInfo(string.Empty, DateTime.MinValue, 0, 0));

        public static async Task<dynamic> CombineWithRemoteResults(this BaseModule module, QueryResult localQueryResult)
        {
            var httpClientFactory = module.HttpClientFactory; // for testing purposes
            var remotes = module.Settings.RemoteInstances;
            var currentRequest = module.Request;
            var negotiator = module.Negotiate;

            if (remotes.Length > 0)
            {
                await UpdateLocalQueryResultWithRemoteData(currentRequest, httpClientFactory, remotes, localQueryResult).ConfigureAwait(false);

                localQueryResult.Messages.Sort(MessageViewComparer.FromRequest(currentRequest));
            }

            return negotiator.WithPartialQueryResult(localQueryResult, currentRequest);
        }

        static async Task UpdateLocalQueryResultWithRemoteData(Request currentRequest, Func<HttpClient> httpClientFactory, string[] remotes, QueryResult localQueryResult)
        {
            var tasks = new List<Task<QueryResult>>(remotes.Length);
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var remote in remotes)
            {
                tasks.Add(FetchAndParse(currentRequest, httpClientFactory, remote));
            }

            var highestTotalCount = localQueryResult.QueryStats.TotalCount;
            var totalCount = localQueryResult.QueryStats.TotalCount;
            var lastModified = DateTime.MinValue;
            var etagBuilder = new StringBuilder();
            foreach (var queryResult in await Task.WhenAll(tasks))
            {
                localQueryResult.Messages.AddRange(queryResult.Messages);

                var header = queryResult.QueryStats;
                totalCount += header.TotalCount;

                if (header.HighestTotalCountOfAllTheInstances > highestTotalCount)
                {
                    highestTotalCount = header.HighestTotalCountOfAllTheInstances;
                }

                if (header.LastModified > lastModified)
                {
                    lastModified = header.LastModified;
                }

                etagBuilder.Append(header.ETag);
            }

            // not exactly beautiful but we are treating localQueryResult as mutable anyway
            localQueryResult.QueryStats = new QueryStatsInfo(etagBuilder.ToString(), lastModified, totalCount, highestTotalCount);
        }

        static async Task<QueryResult> FetchAndParse(Request currentRequest, Func<HttpClient> httpClientFactory, string remoteUri)
        {
            var instanceUri = new Uri($"{remoteUri}{currentRequest.Path}?{currentRequest.Url.Query}");
            var httpClient = httpClientFactory();
            try
            {
                var rawResponse = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, instanceUri)).ConfigureAwait(false);
                // special case - queried by conversation ID and nothing was found
                if (rawResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    return EmptyResult;
                }

                return await ParseResult(rawResponse).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.Warn($"Failed to query remote instance at {remoteUri}.", exception);
                return EmptyResult;
            }
        }

        static async Task<QueryResult> ParseResult(HttpResponseMessage responseMessage)
        {
            using (var responseStream = await responseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var jsonReader = new JsonTextReader(new StreamReader(responseStream)))
            {
                var messages = jsonSerializer.Deserialize<List<MessagesView>>(jsonReader);

                IEnumerable<string> totalCounts;
                var totalCount = 0;
                if (responseMessage.Headers.TryGetValues("Total-Count", out totalCounts))
                {
                    totalCount = int.Parse(totalCounts.ElementAt(0));
                }

                IEnumerable<string> etags;
                string etag = null;
                if (responseMessage.Headers.TryGetValues("ETag", out etags))
                {
                    etag = etags.ElementAt(0);
                }

                IEnumerable<string> lastModifiedValues;
                var lastModified = DateTime.UtcNow;
                if (responseMessage.Headers.TryGetValues("Last-Modified", out lastModifiedValues))
                {
                    lastModified = DateTime.ParseExact(lastModifiedValues.ElementAt(0), "R", CultureInfo.InvariantCulture);
                }

                return new QueryResult(messages, new QueryStatsInfo(etag, lastModified, totalCount, totalCount));
            }
        }
    }
}