namespace ServiceControl.HeartbeatMonitoring
{
    using Contracts.HeartbeatMonitoring;
    using EndpointControl.Contracts;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;

    class MonitoringDisabledForEndpointHandler : IHandleMessages<MonitoringDisabledForEndpoint>
    {
        private readonly IBus bus;
        private readonly IDocumentStore store;
        private readonly HeartbeatStatusProvider statusProvider;

        public MonitoringDisabledForEndpointHandler(IBus bus, IDocumentStore store, HeartbeatStatusProvider statusProvider)
        {
            this.bus = bus;
            this.store = store;
            this.statusProvider = statusProvider;
        }

        public void Handle(MonitoringDisabledForEndpoint message)
        {
            // The user could be disabling an endpoint that had the heartbeats plugin, or not.
            // Check to see if the endpoint had associated heartbeat.
            using (var session = store.OpenSession())
            {
                var heartbeat = session.Load<Heartbeat>(message.EndpointInstanceId);
                if (heartbeat != null)
                {
                    if (heartbeat.Disabled)
                    {
                        Logger.Info($"Heartbeat monitoring for endpoint {message.EndpointInstanceId} is already disabled");
                        return;
                    }
                    heartbeat.Disabled = true;
                    session.Store(heartbeat);
                    session.SaveChanges();
                }
                else
                {
                    Logger.Info($"Heartbeat for endpoint {message.EndpointInstanceId} not found. Possible cause is that the endpoint may not have the plug in installed.");
                }
            }

            statusProvider.DisableMonitoring(message.Endpoint);
            bus.Publish(new HeartbeatMonitoringDisabled
            {
                EndpointInstanceId = message.EndpointInstanceId
            });
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(MonitoringEnabledForEndpointHandler));
    }
}