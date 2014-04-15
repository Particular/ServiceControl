namespace ServiceControl.HeartbeatMonitoring
{
    using Contracts.HeartbeatMonitoring;
    using InternalMessages;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;

    class RegisterPotentiallyMissingHeartbeatsHandler : IHandleMessages<RegisterPotentiallyMissingHeartbeats>
    {
        public IDocumentSession Session { get; set; }
        public IBus Bus { get; set; }
        public HeartbeatStatusProvider StatusProvider { get; set; }

        public void Handle(RegisterPotentiallyMissingHeartbeats message)
        {

            var heartbeat = Session.Load<Heartbeat>(message.EndpointInstanceId);

            if (message.LastHeartbeatAt < heartbeat.LastReportAt)
            {
                Logger.WarnFormat("Heartbeat received after detection, ignoring");
                return;
            }

            if (heartbeat.ReportedStatus == Status.Dead)
            {
                Logger.WarnFormat("Endpoint already reported as inactive");
                return;
            }

            heartbeat.ReportedStatus = Status.Dead;

            Bus.Publish(new EndpointFailedToHeartbeat
            {
                Endpoint = heartbeat.EndpointDetails,
                LastReceivedAt = heartbeat.LastReportAt,
                DetectedAt = message.DetectedAt
            });

            StatusProvider.RegisterEndpointThatFailedToHeartbeat(heartbeat.EndpointDetails);

            Session.Store(heartbeat);
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(RegisterPotentiallyMissingHeartbeatsHandler));
    }
}