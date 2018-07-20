namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using Nancy;

    public abstract class ScatterGatherApiMessageView<TInput> : ScatterGatherApi<TInput, List<MessagesView>>
    {
        protected override List<MessagesView> ProcessResults(Request request, QueryResult<List<MessagesView>>[] results)
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

        private IComparer<MessagesView> FinalOrder(Request request) => MessageViewComparer.FromRequest(request);
    }
}