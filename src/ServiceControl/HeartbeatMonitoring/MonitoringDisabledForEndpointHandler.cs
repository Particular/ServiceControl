namespace ServiceControl.HeartbeatMonitoring
{
    using System;
    using Contracts.HeartbeatMonitoring;
    using EndpointControl.Contracts;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;

    class MonitoringDisabledForEndpointHandler : IHandleMessages<MonitoringDisabledForEndpoint>
    {
        public HeartbeatStatusProvider StatusProvider { get; set; }

        public IDocumentSession Session { get; set; }

        public IBus Bus { get; set; }

        public void Handle(MonitoringDisabledForEndpoint message)
        {
            var heartbeat = Session.Load<Heartbeat>(message.EndpointInstanceId);

            if (heartbeat == null)
            {
                throw new Exception("No heartbeat with found with id: " + message.EndpointInstanceId);
            }

            if (heartbeat.Disabled)
            {
                Logger.InfoFormat("Heartbeat monitoring for endpoint {0} is already disabled", message.EndpointInstanceId);
                return;
            }

            heartbeat.Disabled = true;

            StatusProvider.DisableMonitoring(message.Endpoint);

            Session.Store(heartbeat);

            Bus.Publish(new HeartbeatMonitoringDisabled
            {
                EndpointInstanceId = message.EndpointInstanceId
            });
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(MonitoringEnabledForEndpointHandler));
    }
}