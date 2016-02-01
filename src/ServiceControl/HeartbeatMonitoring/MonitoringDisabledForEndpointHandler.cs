namespace ServiceControl.HeartbeatMonitoring
{
    using System.Threading.Tasks;
    using Contracts.HeartbeatMonitoring;
    using EndpointControl.Contracts;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;

    class MonitoringDisabledForEndpointHandler : IHandleMessages<MonitoringDisabledForEndpoint>
    {
        public HeartbeatStatusProvider StatusProvider { get; set; }

        public IDocumentSession Session { get; set; }


        public Task Handle(MonitoringDisabledForEndpoint message, IMessageHandlerContext context)
        {
            // The user could be disabling an endpoint that had the heartbeats plugin, or not.
            // Check to see if the endpoint had associated heartbeat.
            var heartbeat = Session.Load<Heartbeat>(message.EndpointInstanceId);
            if (heartbeat != null)
            {
                if (heartbeat.Disabled)
                {
                    Logger.InfoFormat("Heartbeat monitoring for endpoint {0} is already disabled", message.EndpointInstanceId);
                    return Task.FromResult(0);
                }
                heartbeat.Disabled = true;
                Session.Store(heartbeat);
            }
            else
            {
                Logger.InfoFormat("Heartbeat for endpoint {0} not found. Possible cause is that the endpoint may not have the plug in installed.", message.EndpointInstanceId);
            }

            StatusProvider.DisableMonitoring(message.Endpoint);
            return context.Publish(new HeartbeatMonitoringDisabled
            {
                EndpointInstanceId = message.EndpointInstanceId
            });
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(MonitoringEnabledForEndpointHandler));
    }
}