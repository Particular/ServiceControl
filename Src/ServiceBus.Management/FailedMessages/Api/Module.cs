namespace ServiceBus.Management.FailedMessages.Api
{
    using System.Linq;
    using Management.Api;
    using Nancy;
    using Raven.Client;
    using RavenDB;

    public class Module : NancyModule
    {
        public IDocumentStore Store { get; set; }

        public Module()
        {
            Get["/failedmessages"] = _ =>
                {
                    using (var session = Store.OpenSession())
                    {
                        RavenQueryStatistics stats;
                        var results = session.Query<Message>()
                            .Statistics(out stats)
                            .Take(50)
                            .ToArray();

                        return Json.Format(results, stats);
                    }
                };

            Get["/endpoints/{name}/failedmessages"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    string endpoint = parameters.name;

                    RavenQueryStatistics stats;
                    var results = session.Query<Message>()
                        .Statistics(out stats)
                        .Where(f => f.Endpoint == endpoint)
                        .Take(50)
                        .ToArray();

                    return Json.Format(results, stats);
                }
            };
        }
    }
}