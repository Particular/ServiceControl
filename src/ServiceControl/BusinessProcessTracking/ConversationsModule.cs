namespace ServiceControl.BusinessProcessTracking
{
    using System.Linq;
    using Infrastructure.Extensions;
    using Infrastructure.Nancy.Modules;
    using Infrastructure.RavenDB.Indexes;
    using MessageAuditing;
    using Nancy;
    using Raven.Client;

    public class ConversationsModule : BaseModule
    {
        public ConversationsModule()
        {
            Get["/conversations/{conversationid}"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    string conversationId = parameters.conversationid;

                    RavenQueryStatistics stats;
                    var results = session.Query<Conversations_Sorted.Result, Conversations_Sorted>()
                        .Statistics(out stats)
                        .Where(m => m.ConversationId == conversationId)
                        .Sort(Request, defaultSortDirection: "asc")
                        .OfType<Message>()
                        .Paging(Request)
                        .ToArray();

                    if (results.Length == 0)
                    {
                        return HttpStatusCode.NotFound;
                    }

                    return Negotiate
                        .WithModelAppendedRestfulUrls(results, Request)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }
            };
        }

        public IDocumentStore Store { get; set; }
    }
}