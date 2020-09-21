namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using Raven.Client.Documents;
    using ServiceBus.Management.Infrastructure.Settings;

    abstract class ScatterGatherApiMessageView<TInput> : ScatterGatherApi<TInput, IList<MessagesView>>
    {
        protected ScatterGatherApiMessageView(IDocumentStore documentStore, Settings settings, Func<HttpClient> httpClientFactory) : base(documentStore, settings, httpClientFactory)
        {
        }

        protected override IList<MessagesView> ProcessResults(HttpRequestMessage request, QueryResult<IList<MessagesView>>[] results)
        {
            var combined = new List<MessagesView>();
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

                combined.AddRange(messagesViews);
            }

            var comparer = FinalOrder(request);
            if (comparer != null)
            {
                combined.Sort(comparer);
            }

            return combined;
        }

        private IComparer<MessagesView> FinalOrder(HttpRequestMessage request) => MessageViewComparer.FromRequest(request);
    }
}