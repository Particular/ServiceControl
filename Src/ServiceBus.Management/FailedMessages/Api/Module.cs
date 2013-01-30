namespace ServiceBus.Management.FailedMessages.Api
{
    using System.Linq;
    using Raven.Client;
    using RavenDB;

    public class Module : Nancy.NancyModule
    {
        
        public Module(): base("/failedmessages")
        {
            Get["/"]= _ =>
                {
                    using (var session = RavenBootstrapper.Store.OpenSession())
                    {
                        RavenQueryStatistics stats;
                        var results = session.Query<FailedMessage>()
                            .Statistics(out stats)
                            .Take(50)
                            .ToArray();

                        return Newtonsoft.Json.JsonConvert.SerializeObject(new
                            {
                                faults = results,
                                total = stats.TotalResults
                            }
                            );
                    }
                };
        }
    }
}