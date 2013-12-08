namespace ServiceBus.Management.BusinessProcessTracking
{
    using System.Linq;
    using Infrastructure.Extensions;
    using Infrastructure.Nancy.Modules;
    using Nancy;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceControl.CompositeViews;

    public class GetMessagesByConversation : BaseModule
    {
        public GetMessagesByConversation()
        {
            Get["/conversations/{conversationid}"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    string conversationId = parameters.conversationid;

                    RavenQueryStatistics stats;
                    var results = session.Query<MessagesView, MessagesViewIndex>()
                        .Statistics(out stats)
                        //.IncludeSystemMessagesWhere(Request)
                        .Where(m => m.ConversationId == conversationId)
                        .Sort(Request)
                        .Paging(Request)
                        .ToArray();

                    if (results.Length == 0)
                    {
                        return HttpStatusCode.NotFound;
                    }

                    return Negotiate
                        .WithModel(results)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }
            };
        }
    }
}