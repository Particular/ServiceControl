namespace ServiceControl.MessageFailures.Api
{
    using System.Linq;
    using Nancy;
    using Raven.Client;
    using Raven.Client.Indexes;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceBus.Management.Infrastructure.RavenDB.Indexes;

    public class GetErrorById : BaseModule
    {

        public GetErrorById()
        {
            Get["/errors/{id}"] = parameters =>
            {
                string messageId = parameters.id;

                using (var session = Store.OpenSession())
                {
                    var message = session.Load<FailedMessage>(messageId);

                    if (message == null)
                    {
                        return HttpStatusCode.NotFound;
                    }

                    return Negotiate.WithModel(message);
                }
            };
        }
        public IDocumentStore Store { get; set; }

    }

}