namespace ServiceControl.HeartbeatMonitoring
{
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using Nancy;

    public class ApiModule : BaseModule
    {
        public HeartbeatsComputation HeartbeatsComputation { get; set; }

        public ApiModule()
        {
            Get["/heartbeats/stats"] = _ =>
            {
                var heartbeatsStats = HeartbeatsComputation.Current;

                return Negotiate.WithModel(new
                {
                    heartbeatsStats.Active,
                    Failing = heartbeatsStats.Dead
                });
            };
        }
    }
}