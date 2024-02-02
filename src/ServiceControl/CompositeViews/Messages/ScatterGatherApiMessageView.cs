namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using Microsoft.AspNetCore.Http;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence.Infrastructure;

    public record ScatterGatherApiMessageViewWithSystemMessagesContext(
        PagingInfo PagingInfo,
        SortInfo SortInfo,
        bool IncludeSystemMessages) : ScatterGatherApiMessageViewContext(PagingInfo, SortInfo);

    public record ScatterGatherApiMessageViewContext(PagingInfo PagingInfo, SortInfo SortInfo) : ScatterGatherContext(PagingInfo);

    abstract class ScatterGatherApiMessageView<TDataStore, TInput> : ScatterGatherApi<TDataStore, TInput, IList<MessagesView>>
        where TInput : ScatterGatherApiMessageViewContext
    {
        protected ScatterGatherApiMessageView(TDataStore dataStore, Settings settings, IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor) : base(dataStore, settings, httpClientFactory, httpContextAccessor)
        {
        }

        protected override IList<MessagesView> ProcessResults(TInput input, QueryResult<IList<MessagesView>>[] results)
        {
            var deduplicated = new Dictionary<string, MessagesView>();
            foreach (var queryResult in results)
            {
                var messagesViews = queryResult?.Results ?? new List<MessagesView>();
                foreach (var result in messagesViews)
                {
                    result.InstanceId ??= queryResult.InstanceId;

                    if (result.BodyUrl != null && !result.BodyUrl.Contains("instance_id"))
                    {
                        result.BodyUrl += $"?instance_id={queryResult.InstanceId}";
                    }

                    //HINT: De-duplicate the results as some messages might be present in multiple instances (e.g. when they initially failed and later were successfully processed)
                    //The Execute method guarantees that the first item in the results collection comes from the main SC instance so the data fetched from failed messages has
                    //precedence over the data from the audit instances.
                    var key = $"{result.ReceivingEndpoint?.Name}-{result.MessageId}";
                    deduplicated.TryAdd(key, result);
                }
            }

            var combined = deduplicated.Values.ToList();
            var comparer = FinalOrder(input.SortInfo);
            if (comparer != null)
            {
                combined.Sort(comparer);
            }

            return combined;
        }

        IComparer<MessagesView> FinalOrder(SortInfo sortInfo) => MessageViewComparer.FromSortInfo(sortInfo);
    }
}