namespace ServiceControl.HeartbeatMonitoring
{
    using System.Linq;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using Nancy;

    public class ApiModule : BaseModule
    {
        public HeartbeatMonitor HeartbeatMonitor { get; set; }

        public ApiModule()
        {
            Get["/heartbeats"] = _ =>
                {
                    var endpointStatus = HeartbeatMonitor.CurrentStatus();
                
                return Negotiate.WithModel(new HeartbeatSummary
                    {
                        ActiveEndpoints = endpointStatus.Count(s=>!s.Failing),
                        NumberOfFailingEndpoints = endpointStatus.Count(s => s.Failing)
                    });
            };
        }
    }
}