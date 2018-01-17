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
    using Raven.Abstractions.Extensions;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceBus.Management.Infrastructure.Settings;

    public abstract class MessageViewQueryAggregatingModule : BaseModule
    {
        static JsonSerializer jsonSerializer = JsonSerializer.Create(JsonNetSerializer.CreateDefault());
        static HttpClient httpClient = new HttpClient();

        string[] remotes;

        protected MessageViewQueryAggregatingModule(Settings settings)
        {
            remotes = settings.RemoteInstances;
        }

        protected async Task<dynamic> CombineWithRemoteResults(IList<MessagesView> localResults, int localTocalCount, string localEtag, DateTime localLastModified)
        {
            //TODO: It was only present in tthe GetByConversationId
            //if (results.Length == 0)
            //{
            //    return HttpStatusCode.NotFound;
            //}

            if (remotes.Length == 0)
            {
                return Negotiate.WithModel(localResults)
                    .WithPagingLinksAndTotalCount(localTocalCount, Request)
                    .WithEtagAndLastModified(localEtag, localLastModified);
            }
            return await DistributeToSecondaries(localResults, localTocalCount).ConfigureAwait(false);
        }

        async Task<dynamic> DistributeToSecondaries(ICollection<MessagesView> localResults, int localTocalCount)
        {
            var totalCountAndEtagAndLastModified = await DistributeQuery(Request, localResults, localTocalCount);

            var sortedResults = SortResults(Request.Query as DynamicDictionary, localResults);

            //Return all the results
            return Negotiate.WithModel(sortedResults)
                .WithPagingLinksAndTotalCount(totalCountAndEtagAndLastModified.Item1, Request)
                .WithDeterministicEtag(totalCountAndEtagAndLastModified.Item2)
                .WithLastModified(totalCountAndEtagAndLastModified.Item3);
        }

        class PartialQueryResult
        {
            public IList<MessagesView> Messages { get; set; }
            public string ETag { get; set; }
            public DateTime LastModified { get; set; }
            public int TotalCount { get; set; }
        }

        async Task<Tuple<int, string, DateTime>> DistributeQuery(Request currentRequest, ICollection<MessagesView> localResults, int localTocalCount)
        {
            var tasks = new List<Task<PartialQueryResult>>(remotes.Length);
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var remote in remotes)
            {
                tasks.Add(FetchAndParse(currentRequest, remote));
            }

            var totalCount = localTocalCount;
            var lastModified = DateTime.MinValue;
            var etagBuilder = new StringBuilder();
            foreach (var queryResult in await Task.WhenAll(tasks))
            {
                foreach (var messagesView in queryResult.Messages)
                {
                    localResults.Add(messagesView);
                }

                totalCount += queryResult.TotalCount;

                if (queryResult.LastModified > lastModified)
                {
                    lastModified = queryResult.LastModified;
                }
                etagBuilder.Append(queryResult.ETag);
            }

            return Tuple.Create(totalCount, etagBuilder.ToString(), lastModified);
        }

        static async Task<PartialQueryResult> FetchAndParse(Request currentRequest, string remoteUri)
        {
            var instanceUri = new Uri($"{remoteUri}{currentRequest.Path}?{currentRequest.Url.Query}");
            var rawResponse = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, instanceUri)).ConfigureAwait(false);
            return await ParseResult(rawResponse).ConfigureAwait(false);
        }

        static async Task<PartialQueryResult> ParseResult(HttpResponseMessage responseMessage)
        {
            using (var responseStream = await responseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var jsonReader = new JsonTextReader(new StreamReader(responseStream)))
            {
                var messages = jsonSerializer.Deserialize<MessagesView[]>(jsonReader);
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
        }

        static MessagesView[] SortResults(DynamicDictionary queryString, ICollection<MessagesView> combinedMessages)
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