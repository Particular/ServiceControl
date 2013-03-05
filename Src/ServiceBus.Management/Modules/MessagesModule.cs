namespace ServiceBus.Management.Modules
{
    using System.Linq;
    using Nancy;
    using Raven.Client;

    public class MessagesModule : NancyModule
    {
        public IDocumentStore Store { get; set; }

        public MessagesModule()
        {
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

            Get["/endpoints"] = parameters =>
                {
                    using (var session = Store.OpenSession())
                    {
                        var endpoints = session.Query<Message>()
                                               .Select(m => m.OriginatingEndpoint)
                                               .Distinct()
                                               .ToArray();

                        return Negotiate.WithModel(endpoints);
                    }
                };
        }
    }
}