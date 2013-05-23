namespace ServiceBus.Management.Modules
{
    using System.Linq;
    using Extensions;
    using Nancy;
    using Raven.Client;

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
                        var results = session.Query<Message>()
                                             .Statistics(out stats)
                                             .Where(m => m.ConversationId == conversationId && m.Status != MessageStatus.RetryIssued)
                                             .Sort(Request, defaultSortDirection: "asc")
                                             .Paging(Request)
                                             .ToArray();

                        return Negotiate
                            .WithModel(results)
                            .WithTotalCount(stats);

                    }
                };
        }
    }
 
}