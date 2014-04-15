namespace ServiceControl.HeartbeatMonitoring
{
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using Nancy;

    public class ApiModule : BaseModule
    {
        public HeartbeatStatusProvider StatusProvider { get; set; }

        public ApiModule()
        {
            Get["/heartbeats/stats"] = _ =>
            {
                var heartbeatsStats = StatusProvider.GetHeartbeatsStats();

                return Negotiate.WithModel(new
                {
                    heartbeatsStats.Active,
                    Failing = heartbeatsStats.Dead
                });
            };
        }
    }
}