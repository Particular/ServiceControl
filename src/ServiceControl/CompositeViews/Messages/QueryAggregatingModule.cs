namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Nancy;
    using Newtonsoft.Json;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceBus.Management.Infrastructure.Settings;

    public abstract class QueryAggregatingModule : BaseModule
    {
        static JsonSerializer jsonSerializer = JsonSerializer.Create(JsonNetSerializer.CreateDefault());
        static HttpClient httpClient = new HttpClient();

        string[] remotes;

        protected QueryAggregatingModule(Settings settings)
        {
            remotes = settings.RemoteInstances;
        }

        public async Task<dynamic> CombineWithRemoteResults(IList<MessagesView> localResults, RavenQueryStatistics localStats)
        {
            //TODO: It was only present in tthe GetByConversationId
            //if (results.Length == 0)
            //{
            //    return HttpStatusCode.NotFound;
            //}

            if (remotes.Length == 0)
            {
                return Negotiate.WithModel(localResults)
                    .WithPagingLinksAndTotalCount(localStats, Request)
                    .WithEtagAndLastModified(localStats);
            }
            return await DistributeToSecondaries(localResults, localStats);
        }

        async Task<dynamic> DistributeToSecondaries(IList<MessagesView> localResults, RavenQueryStatistics localStats)
        {
            var queryResults = await DistributeQuery(Request);

            queryResults.Insert(0, new PartialQueryResult
            {
                Messages = localResults,
                TotalCount = localStats.TotalResults
            });

            //Combine all the results
            var aggregatedResults = SortResults(Request.Query as DynamicDictionary, queryResults.SelectMany(r => r.Messages));

            //Return all the results
            return Negotiate.WithModel(aggregatedResults)
                .WithPagingLinksAndTotalCount(queryResults.Sum(r => r.TotalCount), Request)
                .WithDeterministicEtag(string.Join(string.Empty, queryResults.Select(r => r.ETag)))
                .WithLastModified(queryResults.Select(r => r.LastModified).Max());
        }

        class PartialQueryResult
        {
            public IList<MessagesView> Messages { get; set; }
            public string ETag { get; set; }
            public DateTime LastModified { get; set; }
            public int TotalCount { get; set; }
        }

        async Task<List<PartialQueryResult>> DistributeQuery(Request currentRequest)
        {
            var queryTasks = remotes
                .Select(instanceUrl => new Uri($"{instanceUrl}{currentRequest.Path}?{currentRequest.Url.Query}"))
                .Select(requestUri => new HttpRequestMessage(HttpMethod.Get, requestUri))
                .Select(async request =>
                {
                    var rawResponse = await httpClient.SendAsync(request).ConfigureAwait(false);
                    return await ParseResult(rawResponse).ConfigureAwait(false);
                });

            var responses = await Task.WhenAll(queryTasks);
            return responses.ToList();
        }

        static async Task<PartialQueryResult> ParseResult(HttpResponseMessage responseMessage)
        {
            var responseStream = await responseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false);
            var jsonReader = new JsonTextReader(new StreamReader(responseStream));
            var messages = jsonSerializer.Deserialize<List<MessagesView>>(jsonReader);

            var totalCount = responseMessage.Headers.GetValues("Total-Count").Select(int.Parse).Cast<int?>().FirstOrDefault() ?? -1;
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

            return new PartialQueryResult
            {
                Messages = messages,
                TotalCount = totalCount,
                ETag = etag,
                LastModified = lastModified
            };
        }

        static MessagesView[] SortResults(DynamicDictionary queryString, IEnumerable<MessagesView> combinedMessages)
        {
            //Set the default sort to time_sent if not already set
            string sortBy = queryString.ContainsKey("sort")
                ? queryString["sort"]
                : "time_sent";

            //Set the default sort direction to `desc` if not already set
            string sortOrder = queryString.ContainsKey("direction")
                ? queryString["direction"]
                : "desc";

            Func<MessagesView, IComparable> keySelector = m => m.TimeSent;
            switch (sortBy)
            {
                case "id":
                case "message_id":
                    keySelector = m => m.MessageId;
                    break;

                case "message_type":
                    keySelector = m => m.MessageType;
                    break;

                case "critical_time":
                    keySelector = m => m.CriticalTime;
                    break;

                case "delivery_time":
                    keySelector = m => m.DeliveryTime;
                    break;

                case "processing_time":
                    keySelector = m => m.ProcessingTime;
                    break;

                case "processed_at":
                    keySelector = m => m.ProcessedAt;
                    break;

                case "status":
                    keySelector = m => m.Status;
                    break;
            }

            return sortOrder == "asc"
                ? combinedMessages.OrderBy(keySelector).ToArray()
                : combinedMessages.OrderByDescending(keySelector).ToArray();
        }
    }
}