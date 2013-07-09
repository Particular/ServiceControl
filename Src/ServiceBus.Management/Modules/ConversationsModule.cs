namespace ServiceBus.Management.Modules
{
    using System.Linq;
    using Extensions;
    using Nancy;
    using Raven.Client;
    using RavenDB.Indexes;

    public class ConversationsModule : NancyModule
    {
        public IDocumentStore Store { get; set; }

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
                                             .Where(m => m.ConversationId == conversationId && m.Status != MessageStatus.RetryIssued)
                                             .Sort(Request, defaultSortDirection: "asc")
                                             .OfType<Message>()
                                             .Paging(Request)
                                             .ToArray();

                        if (results.Length == 0)
                        {
                            return HttpStatusCode.NotFound;
                        }

                        return Negotiate
                            .WithModel(results)
                            .WithTotalCount(stats);

                    }
                };
        }
    }
 
}