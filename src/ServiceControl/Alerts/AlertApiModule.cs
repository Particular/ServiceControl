namespace ServiceControl.Alerts
{
    using System.Linq;
    using Nancy;
    using NServiceBus;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
 
    public class AlertApiModule : BaseModule
    {
        public IDocumentStore Store { get; set; }
        public IBus Bus { get; set; }

        public AlertApiModule()
        {
            Get["/alerts"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    RavenQueryStatistics stats;
                    var results = session.Query<Alert>()
                        .Statistics(out stats)
                        .ToArray();

                    return Negotiate.WithModel(results)
                        .WithEtagAndLastModified(stats);
                }
            };
        }
    }
}
