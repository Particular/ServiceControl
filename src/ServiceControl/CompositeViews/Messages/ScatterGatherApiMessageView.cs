namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using Nancy;

    public abstract class ScatterGatherApiMessageView<TInput> : ScatterGatherApi<TInput, MessagesView>
    {
        protected override void ProcessResults(Request request, List<MessagesView> results)
        {
            var comparer = FinalOrder(request);
            if(comparer != null)
                results.Sort(comparer);
        }

        private IComparer<MessagesView> FinalOrder(Request request) => MessageViewComparer.FromRequest(request);
    }
}