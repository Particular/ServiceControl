namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Infrastructure.Extensions;
    using Nancy;
    using Newtonsoft.Json;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class GetMessages : BaseModule
    {
        static JsonSerializer jsonSerializer = JsonSerializer.Create(JsonNetSerializer.CreateDefault());
        static HttpClient httpClient = new HttpClient();
        static string[] remotes = {"http://localhost:33334/api"};

        public GetMessages()
        {
            Get["/messages", true] = async (parameters, token) =>
            {
                MessagesView[] localMessages;
                RavenQueryStatistics stats;
                using (var session = Store.OpenSession())
                {
                    localMessages = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                        .IncludeSystemMessagesWhere(Request)
                        .Statistics(out stats)
                        .Sort(Request)
                        .Paging(Request)
                        .TransformWith<MessagesViewTransformer, MessagesView>()
                        .ToArray();
                }

                // Return if no secondaries
                if (remotes.Length == 0)
                {
                    return Negotiate.WithModel(localMessages)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }

                var queryResults = await DistributeQuery(Request);

                queryResults.Insert(0, new PartialQueryResult
                {
                    Messages = localMessages,
                    TotalCount = stats.TotalResults
                });

                //Combine all the results
                var aggregatedResults = AggregateResults(Request, queryResults.SelectMany(r => r.Messages));

                //Return all the results
                return Negotiate.WithModel(aggregatedResults)
                    .WithPagingLinksAndTotalCount(queryResults.Sum(r => r.TotalCount), Request);
            };


            Get["/endpoints/{name}/messages"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    string endpoint = parameters.name;

                    RavenQueryStatistics stats;
                    var results = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                        .IncludeSystemMessagesWhere(Request)
                        .Where(m => m.ReceivingEndpointName == endpoint)
                        .Statistics(out stats)
                        .Sort(Request)
                        .Paging(Request)
                        .TransformWith<MessagesViewTransformer, MessagesView>()
                        .ToArray();

                    return Negotiate
                        .WithModel(results)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }
            };
        }

        class PartialQueryResult
        {
            public MessagesView[] Messages { get; set; }
            public int TotalCount { get; set; }
        }

        async Task<List<PartialQueryResult>> DistributeQuery(Request currentRequest)
        {
            var queryTasks = remotes
                .Select(instanceUrl => new Uri($"{instanceUrl}{currentRequest.Path}?{currentRequest.Url.Query}"))
                .Select(requestUri => new HttpRequestMessage(HttpMethod.Get, requestUri))
                .Select(async request =>
                {
                    var rawResponse = await httpClient.SendAsync(request);
                    return await ParseResult(rawResponse);
                });

            var responses = await Task.WhenAll(queryTasks);
            return responses.ToList();
        }

        static async Task<PartialQueryResult> ParseResult(HttpResponseMessage responseMessage)
        {
            var responseStream = await responseMessage.Content.ReadAsStreamAsync();
            var jsonReader = new JsonTextReader(new StreamReader(responseStream));
            var messages = jsonSerializer.Deserialize<MessagesView[]>(jsonReader);

            var totalCount = responseMessage.Headers.GetValues("Total-Count").Select(int.Parse).Cast<int?>().FirstOrDefault() ?? -1;

            return new PartialQueryResult
            {
                Messages = messages,
                TotalCount = totalCount
            };
        }

        MessagesView[] AggregateResults(dynamic queryString, IEnumerable<MessagesView> combinedMessages)
        {
            //Set the default sort to time_sent if not already set
            string sortBy = queryString.sort_by.HasValue
                ? queryString.sort_by
                : "time_sent";

            //Set the default sort direction to `desc` if not already set
            string sortOrder = queryString.sort_order.HasValue
                ? queryString.sort_order
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