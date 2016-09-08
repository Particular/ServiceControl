namespace ServiceControl.HeartbeatMonitoring
{
    using Contracts.HeartbeatMonitoring;
    using EndpointControl.Contracts;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;

    class MonitoringEnabledForEndpointHandler : IHandleMessages<MonitoringEnabledForEndpoint>
    {
        private readonly IBus bus;
        private readonly IDocumentStore store;
        private readonly HeartbeatStatusProvider statusProvider;

        public MonitoringEnabledForEndpointHandler(IBus bus, IDocumentStore store, HeartbeatStatusProvider statusProvider)
        {
            this.bus = bus;
            this.store = store;
            this.statusProvider = statusProvider;
        }

        public void Handle(MonitoringEnabledForEndpoint message)
        {
            // The user could be disabling an endpoint that had the heartbeats plugin, or not.
            // Check to see if the endpoint had associated heartbeat.
            using (var session = store.OpenSession())
            {
                var heartbeat = session.Load<Heartbeat>(message.EndpointInstanceId);

                if (heartbeat != null)
                {
                    if (!heartbeat.Disabled)
                    {
                        Logger.Info($"Heartbeat monitoring for endpoint {message.EndpointInstanceId} is already enabled");
                        return;
                    }
                    heartbeat.Disabled = false;
                    session.Store(heartbeat);
                    session.SaveChanges();
                }
                else
                {
                    Logger.Info($"Heartbeat for endpoint {message.EndpointInstanceId} not found. Possible cause is that the endpoint may not have the plug in installed.");
                }
            }

            statusProvider.EnableMonitoring(message.Endpoint);
            bus.Publish(new HeartbeatMonitoringEnabled
            {
                EndpointInstanceId = message.EndpointInstanceId
            });
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(MonitoringEnabledForEndpointHandler));
    }
}