namespace ServiceBus.Management.FailedMessages.Api
{
    using System.Linq;
    using Nancy;
    using Raven.Client;
    using RavenDB;

    public class Module : NancyModule
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

                        var response = (Response) Newtonsoft.Json.JsonConvert.SerializeObject(results);

                        response.ContentType = "application/json";
                        response.Headers["X-TotalCount"] = stats.TotalResults.ToString();
                        
                        return response;
                    }
                };
        }
    }
}