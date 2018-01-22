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
    using Nancy.Responses.Negotiation;
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

        static PartialQueryResult EmptyResult = new PartialQueryResult
        {
            Messages = new List<MessagesView>(),
            Header = new HeaderInfo(string.Empty, DateTime.MinValue, 0)
        };

        public static async Task<dynamic> CombineWithRemoteResults(this BaseModule module, IList<MessagesView> localResults, int localTocalCount, string localEtag, DateTime localLastModified)
        {
            var httpClientFactory = module.HttpClientFactory; // for testing purposes
            var remotes = module.Settings.RemoteInstances;
            var currentRequest = module.Request;
            var negotiator = module.Negotiate;
            if (remotes.Length == 0)
            {
                return negotiator.WithModel(localResults)
                    .WithPagingLinksAndTotalCount(localTocalCount, currentRequest)
                    .WithEtagAndLastModified(localEtag, localLastModified);
            }

            return await QueryRemoteInstances(currentRequest, negotiator, httpClientFactory, remotes, localResults, localTocalCount).ConfigureAwait(false);
        }

        static async Task<dynamic> QueryRemoteInstances(Request currentRequest, Negotiator negotiator, Func<HttpClient> httpClientFactory, string[] remotes, ICollection<MessagesView> localResults, int localTocalCount)
        {
            var headerInfo = await Query(currentRequest, httpClientFactory, remotes, localResults, localTocalCount).ConfigureAwait(false);

            var sortedResults = SortResults(currentRequest.Query as DynamicDictionary, localResults);

            //Return all the results
            var numberOfInstances = remotes.Length + 1;

            return negotiator.WithModel(sortedResults)
                .WithPagingLinksAndTotalCount(headerInfo.TotalCount, numberOfInstances, currentRequest)
                .WithDeterministicEtag(headerInfo.ETag)
                .WithLastModified(headerInfo.LastModified);
        }

        static async Task<HeaderInfo> Query(Request currentRequest, Func<HttpClient> httpClientFactory, string[] remotes, ICollection<MessagesView> localResults, int localTocalCount)
        {
            var tasks = new List<Task<PartialQueryResult>>(remotes.Length);
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var remote in remotes)
            {
                tasks.Add(FetchAndParse(currentRequest, httpClientFactory, remote));
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

                var header = queryResult.Header;
                totalCount += header.TotalCount;

                if (header.LastModified > lastModified)
                {
                    lastModified = header.LastModified;
                }

                etagBuilder.Append(header.ETag);
            }

            return new HeaderInfo(etagBuilder.ToString(), lastModified, totalCount);
        }

        static async Task<PartialQueryResult> FetchAndParse(Request currentRequest, Func<HttpClient> httpClientFactory, string remoteUri)
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
                    Header = new HeaderInfo(etag, lastModified, totalCount)
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

        class PartialQueryResult
        {
            public IList<MessagesView> Messages { get; set; }
            public HeaderInfo Header { get; set; }
        }

        struct HeaderInfo
        {
            public readonly string ETag;
            public readonly DateTime LastModified;
            public readonly int TotalCount;

            public HeaderInfo(string eTag, DateTime lastModified, int totalCount)
            {
                ETag = eTag;
                LastModified = lastModified;
                TotalCount = totalCount;
            }
        }
    }
}