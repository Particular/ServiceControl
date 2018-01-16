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
        static string[] secondaries = {"http://localhost:33334/api"};

        public GetMessages()
        {
            Get["/messages", true] = async (parameters, token) =>
            {
                var queryString = Request.Query;

                var pageSize = 50; //SC default value
                if (queryString.per_page.HasValue)
                {
                    pageSize = int.Parse(queryString.per_page);
                }
                var offsetValues = new int[secondaries.Length + 1];
                if (queryString.offset.HasValue)
                {
                    string offset = queryString.offset;
                    string[] offsetsSplit = offset.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    offsetValues = offsetsSplit.Select(int.Parse).ToArray();
                    var pageNumber = offsetValues[0] / pageSize + 1;
                    queryString.page = pageNumber;
                }

                MessagesView[] masterResults;
                RavenQueryStatistics stats;
                using (var session = Store.OpenSession())
                {
                    masterResults = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                        .IncludeSystemMessagesWhere(Request)
                        .Statistics(out stats)
                        .Sort(Request)
                        .Paging(Request)
                        .TransformWith<MessagesViewTransformer, MessagesView>()
                        .ToArray();
                }

                if (secondaries.Length == 0)
                {
                    return Negotiate.WithModel(masterResults)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }

                var browseRight = offsetValues[0] % pageSize == 0;

                string sortBy = queryString.sort_by.HasValue
                    ? queryString.sort_by
                    : "time_sent";

                string sortOrder = queryString.sort_order.HasValue
                    ? queryString.sort_order
                    : "desc";

                var slaveOffsets = offsetValues.Skip(1).ToArray();
                var slaveResults = await DistributeQuery(pageSize, sortBy, sortOrder, slaveOffsets, Request.Path);

                var allResult = slaveResults.ToList();
                allResult.Insert(0, new PartialQueryResult
                {
                    Messages = masterResults,
                    StartOffset = offsetValues[0],
                    TotalCount = stats.TotalResults
                });

                var aggregatedResults = AggregateResults(pageSize, sortBy, sortOrder, browseRight, allResult);

                var newOffsets = aggregatedResults.Item2;
                var prevPageOffsets = !browseRight ? newOffsets : offsetValues.Select(x => x -1);
                var nextPageOffsets = browseRight ? newOffsets : offsetValues.Select(x => x + 1);

                return Negotiate.WithModel(aggregatedResults.Item1)
                    .WithPagingLinksAndTotalCount(sortBy, sortOrder, pageSize, allResult.Select(x => x.TotalCount), prevPageOffsets, nextPageOffsets, Request);
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
            public int StartOffset { get; set; }
            public MessagesView[] Messages { get; set; }
            public int TotalCount { get; set; }
        }

        async Task<PartialQueryResult[]> DistributeQuery(int pageSize, string sort, string direction, int[] offsetArray, string urlSuffix)
        {
            var queryTasks = secondaries.Zip(offsetArray, (url, offset) => new { url, offset })
                .Select(x => new { x.offset, request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(x.url, urlSuffix, x.offset, pageSize, sort, direction)) })
                .Select(async x =>
                {
                    var rawResponse = await httpClient.SendAsync(x.request);
                    var result = await ParseResult(rawResponse, x.offset);
                    return result;
                })
                .ToArray();

            var responses = await Task.WhenAll(queryTasks);
            return responses;
        }

        static Uri BuildUrl(string prefix, string suffix, int offset, int pageSize, string sort, string direction)
        {
            var pageNumber = offset / pageSize + 1;
            var uri = new Uri($"{prefix}{suffix}?page={pageNumber}&per_page={pageSize}&direction={direction}&sort={sort}");
            return uri;
        }

        static async Task<PartialQueryResult> ParseResult(HttpResponseMessage responseMessage, int index)
        {
            string body = await responseMessage.Content.ReadAsStringAsync();

            var responseStream = await responseMessage.Content.ReadAsStreamAsync();
            var jsonReader = new Newtonsoft.Json.JsonTextReader(new StreamReader(responseStream));
            var messages = jsonSerializer.Deserialize<MessagesView[]>(jsonReader);



            var totalCount = responseMessage.Headers.GetValues("Total-Count").Select(int.Parse).Cast<int?>().FirstOrDefault() ?? -1;

            return new PartialQueryResult
            {
                Messages = messages,
                TotalCount = totalCount,
                StartOffset = index
            };
        }

        Tuple<MessagesView[], int[]> AggregateResults(int pageSize, string sort, string sortDirection, bool browseRight, List<PartialQueryResult> partialResults)
        {
            var heads = new MessagesView[partialResults.Count];
            var localOffsets = partialResults.Select(x => x.StartOffset % pageSize).ToArray();
            var newOffsets = partialResults.Select(x => x.StartOffset).ToArray();
            Func<MessagesView, IComparable> keySelector = m => m.TimeSent;
            switch (sort)
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

            var results = new List<MessagesView>();
            for (var i = 0; i < pageSize; i++)
            {
                for (var headIndex = 0; headIndex < heads.Length; headIndex++)
                {
                    //Replenish
                    if (heads[headIndex] == null)
                    {
                        if (localOffsets[headIndex] < partialResults[headIndex].Messages.Length && localOffsets[headIndex] >= 0)
                        {
                            var messageToken = partialResults[headIndex].Messages[localOffsets[headIndex]];
                            heads[headIndex] = messageToken;
                        }
                    }
                }
                //Find one with smallest/largest value depending on sort direction
                Func<int, bool> comparison;
                if (sortDirection == "asc" && browseRight || sortDirection == "desc" && !browseRight)
                {
                    comparison = x => x < 0;
                }
                else
                {
                    comparison = x => x > 0;
                }
                var next = Min(heads, keySelector, comparison);
                if (next < 0) //No more messages
                {
                    break;
                }
                results.Add(partialResults[next].Messages[localOffsets[next]]);

                var offsetIncrement = browseRight ? 1 : -1;

                localOffsets[next] += offsetIncrement;
                newOffsets[next] += offsetIncrement;
                heads[next] = null;
            }

            return Tuple.Create(results.ToArray(), newOffsets);
        }

        static int Min<T>(MessagesView[] messages, Func<MessagesView, T> sortOrder, Func<int, bool> comparison)
            where T : IComparable
        {
            IComparable minValue = default(T);
            var anyValue = false;
            var minIndex = -1;

            for (var i = 0; i < messages.Length; i++)
            {
                if (messages[i] == null)
                {
                    continue;
                }
                var value = sortOrder(messages[i]);
                if (!anyValue || comparison(value.CompareTo(minValue)))
                {
                    minValue = value;
                    minIndex = i;
                }
                anyValue = true;
            }
            return minIndex;
        }
    }
}