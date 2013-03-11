namespace ServiceBus.Management.Modules
{
    using System.Linq;
    using Nancy;
    using Raven.Client;
    using RavenDB.Indexes;

    public class EndpointModule : NancyModule
    {
        public IDocumentStore Store { get; set; }

        public EndpointModule()
        {
            Get["/endpoints"] = parameters =>
                {
                    using (var session = Store.OpenSession())
                    {
                        var endpoints = session.Query<Endpoints_Distinct.Result, Endpoints_Distinct>()
                                               .Select(r => r.Endpoint)
                                               .ToArray();

                        return Negotiate.WithModel(endpoints);
                    }
                };
        }
    }
}