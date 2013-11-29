namespace ServiceControl.EventLog
{
    using System.Linq;
    using Nancy;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class EventLogApiModule : BaseModule
    {
        public IDocumentStore Store { get; set; }

        public EventLogApiModule()
        {
            Get["/eventlogitems"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    RavenQueryStatistics stats;
                    var results = session.Query<EventLogItem>()
                        .Statistics(out stats)
                        .ToArray();

                    return Negotiate.WithModel(results)
                        .WithEtagAndLastModified(stats);
                }
            };
        }
    }
}
