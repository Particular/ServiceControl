namespace ServiceControl.HeartbeatMonitoring
{
    using Contracts.HeartbeatMonitoring;
    using InternalMessages;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;

    class RegisterPotentiallyMissingHeartbeatsHandler : IHandleMessages<RegisterPotentiallyMissingHeartbeats>
    {
        private readonly IBus bus;
        private readonly IDocumentStore store;
        private readonly HeartbeatStatusProvider statusProvider;

        public RegisterPotentiallyMissingHeartbeatsHandler(IBus bus, IDocumentStore store, HeartbeatStatusProvider statusProvider)
        {
            this.bus = bus;
            this.store = store;
            this.statusProvider = statusProvider;
        }

        public void Handle(RegisterPotentiallyMissingHeartbeats message)
        {
            Heartbeat heartbeat;

            using (var session = store.OpenSession())
            {
                heartbeat = session.Load<Heartbeat>(message.EndpointInstanceId);

                if (heartbeat == null)
                {
                    Logger.Debug("Heartbeat not saved in database yet, will retry again.");
                    return;
                }

                if (message.LastHeartbeatAt < heartbeat.LastReportAt)
                {
                    Logger.Debug("Heartbeat received after detection, ignoring");
                    return;
                }

                if (heartbeat.ReportedStatus == Status.Dead)
                {
                    Logger.Debug("Endpoint already reported as inactive");
                    return;
                }

                heartbeat.ReportedStatus = Status.Dead;

                session.Store(heartbeat);
                session.SaveChanges();
            }

            statusProvider.RegisterEndpointThatFailedToHeartbeat(heartbeat.EndpointDetails);

            bus.Publish(new EndpointFailedToHeartbeat
            {
                Endpoint = heartbeat.EndpointDetails,
                LastReceivedAt = heartbeat.LastReportAt,
                DetectedAt = message.DetectedAt
            });
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(RegisterPotentiallyMissingHeartbeatsHandler));
    }
}