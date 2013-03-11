namespace ServiceBus.Management.Modules
{
    using System.Linq;
    using Extensions;
    using Nancy;
    using Raven.Client;
    using RavenDB.Indexes;

    public class MessagesModule : NancyModule
    {
        public IDocumentStore Store { get; set; }

        public MessagesModule()
        {
            Get["/messages/search/{keyword}"] = parameters =>
            {
                string keyword = parameters.keyword;

                using (var session = Store.OpenSession())
                {
                    RavenQueryStatistics stats;
                    var results = session.Query<Messages_Search.Result, Messages_Search>()
                                         .Statistics(out stats)
                                         .Search(s => s.Query, keyword)
                                         .As<Message>()
                                         .Paging(Request)
                                         .ToArray();

                    return Negotiate.WithModel(results)
                                    .WithTotalCount(stats);
                }
            };

            Get["/messages/{id}"] = parameters =>
                {
                    string messageId = parameters.id;

                    using (var session = Store.OpenSession())
                    {
                        var message = session.Load<Message>(messageId);

                        if (message == null)
                        {
                            return new Response {StatusCode = HttpStatusCode.NotFound};
                        }

                        return Negotiate.WithModel(message);
                    }
                };
        }
    }
}