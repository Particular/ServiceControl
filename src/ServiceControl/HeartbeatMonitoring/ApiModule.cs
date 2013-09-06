namespace ServiceControl.HeartbeatMonitoring
{
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using Nancy;
    using System.Linq;

    public class ApiModule : BaseModule
    {
        public HeartbeatMonitor HeartbeatMonitor { get; set; }

        public ApiModule()
        {
            Get["/heartbeats/stats"] = _ =>
            {
                var endpointStatus = HeartbeatMonitor.HeartbeatStatuses;

                return Negotiate.WithModel(new
                {
                    ActiveEndpoints = endpointStatus.Count(s => s.Active),
                    FailingEndpoints = endpointStatus.Count(s => !s.Active)
                }).WithHeader("Cache-Control", "private, max-age=0, must-revalidate");
            };

            Get["/heartbeats"] = _ =>
            {
                var endpointStatus = HeartbeatMonitor.HeartbeatStatuses;

                return Negotiate.WithModel(endpointStatus)
                    .WithHeader("Cache-Control", "private, max-age=0, must-revalidate");
            };
        }
    }
}