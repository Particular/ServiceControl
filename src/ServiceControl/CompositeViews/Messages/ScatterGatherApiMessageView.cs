namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Linq;
    using Nancy;

    public abstract class ScatterGatherApiMessageView<TInput> : ScatterGatherApi<TInput, List<MessagesView>>
    {
        protected override List<MessagesView> ProcessResults(Request request, QueryResult<List<MessagesView>>[] results)
        {
            var combined = results.SelectMany(x => x.Results).ToList();
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