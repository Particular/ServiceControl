namespace ServiceBus.Management.AuditMessages
{
    using System.Linq;
    using Nancy;
    using Raven.Client;

    public class AuditMessagesModule : NancyModule
    {
        public IDocumentStore Store { get; set; }


        public AuditMessagesModule()
        {
            Get["/auditmessages"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {

                    RavenQueryStatistics stats;
                    var results = session.Query<Message>()
                        .Statistics(out stats)
                        .Where(m => m.Status == MessageStatus.Successfull)
                        .OrderBy(m => m.TimeSent)
                        .Take(50)
                        .ToArray();



                    return Negotiate
                            .WithModel(results)
                            .WithHeader("Total-Count", stats.TotalResults.ToString());
                }
            };


            Get["/messages/{id}"] = parameters =>
                {
                    string messageId = parameters.id;

                    using (var session = Store.OpenSession())
                    {
                        var message = session.Load<Message>(messageId);

                        if (message == null)
                            return new Response {StatusCode = HttpStatusCode.NotFound};

                        return Negotiate.WithModel(message);
                    }
                };

            Get["/endpoints/{name}/auditmessages"] = parameters =>
                {
                    using (var session = Store.OpenSession())
                    {
                        string endpoint = parameters.name;

                        RavenQueryStatistics stats;
                        var results = session.Query<Message>()
                            .Statistics(out stats)
                            .Where(m => m.OriginatingEndpoint.Name == endpoint && m.Status == MessageStatus.Successfull)
                            .OrderBy(m => m.TimeSent)
                            .Take(50)
                            .ToArray();



                        return Negotiate
                                .WithModel(results)
                                .WithHeader("Total-Count", stats.TotalResults.ToString());
                    }
                };
        }
    }
}