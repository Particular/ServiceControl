namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

    abstract class ScatterGatherApiMessageView<TInput> : ScatterGatherApi<TInput, IList<MessagesView>>
    {
        protected ScatterGatherApiMessageView(IDocumentStore documentStore, Settings settings, Func<HttpClient> httpClientFactory) : base(documentStore, settings, httpClientFactory)
        {
        }

        protected override IList<MessagesView> ProcessResults(HttpRequestMessage request, QueryResult<IList<MessagesView>>[] results)
        {
            var deduplicated = new Dictionary<string, MessagesView>();
            foreach (var queryResult in results)
            {
                var messagesViews = queryResult?.Results ?? new List<MessagesView>();
                foreach (var result in messagesViews)
                {
                    if (result.InstanceId == null)
                    {
                        result.InstanceId = queryResult.InstanceId;
                    }

                    if (result.BodyUrl != null && !result.BodyUrl.Contains("instance_id"))
                    {
                        result.BodyUrl += $"?instance_id={queryResult.InstanceId}";
                    }
                }

                //HINT: De-duplicate the results as some messages might be present in multiple instances (e.g. when they initially failed and later were successfully processed)
                foreach (MessagesView messagesView in messagesViews)
                {
                    if (!deduplicated.ContainsKey(messagesView.MessageId))
                    {
                        deduplicated.Add(messagesView.Id, messagesView);
                    }
                }
            }

            var combined = deduplicated.Values.ToList();
            var comparer = FinalOrder(request);
            if (comparer != null)
            {
                combined.Sort(comparer);
            }

            return combined;
        }

        IComparer<MessagesView> FinalOrder(HttpRequestMessage request) => MessageViewComparer.FromRequest(request);
    }
}