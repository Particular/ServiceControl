namespace ServiceBus.Management.FailedMessages.Api
{
    using System.Linq;
    using Management.Api;
    using Nancy;
    using Raven.Client;
    using RavenDB;

    public class Module : NancyModule
    {
        
        public Module()
        {
            Get["/failedmessages"] = _ =>
                {
                    using (var session = RavenBootstrapper.Store.OpenSession())
                    {
                        RavenQueryStatistics stats;
                        var results = session.Query<FailedMessage>()
                            .Statistics(out stats)
                            .Take(50)
                            .ToArray();

                        return Json.Format(results, stats);
                    }
                };

            Get["/endpoints/{name}/failedmessages"] = parameters =>
            {
                using (var session = RavenBootstrapper.Store.OpenSession())
                {
                    string endpoint = parameters.name;

                    RavenQueryStatistics stats;
                    var results = session.Query<FailedMessage>()
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