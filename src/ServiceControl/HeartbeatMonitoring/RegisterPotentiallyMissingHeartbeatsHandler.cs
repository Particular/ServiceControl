namespace ServiceControl.HeartbeatMonitoring
{
    using System.Threading.Tasks;
    using Contracts.HeartbeatMonitoring;
    using InternalMessages;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;

    class RegisterPotentiallyMissingHeartbeatsHandler : IHandleMessages<RegisterPotentiallyMissingHeartbeats>
    {
        public IDocumentSession Session { get; set; }
        public HeartbeatStatusProvider StatusProvider { get; set; }

        public async Task Handle(RegisterPotentiallyMissingHeartbeats message, IMessageHandlerContext context)
        {
            var heartbeat = Session.Load<Heartbeat>(message.EndpointInstanceId);

            if (heartbeat == null)
            {
                Logger.DebugFormat("Heartbeat not saved in database yet, will retry again.");
                return;
            }

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

            await context.Publish(new EndpointFailedToHeartbeat
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