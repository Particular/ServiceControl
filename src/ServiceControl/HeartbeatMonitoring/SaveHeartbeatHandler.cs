namespace ServiceControl.HeartbeatMonitoring
{
    using Contracts.HeartbeatMonitoring;
    using Contracts.Operations;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.Logging;
    using Plugin.Heartbeat.Messages;
    using Raven.Client;

    class SaveHeartbeatHandler : IHandleMessages<EndpointHeartbeat>
    {
        public IDocumentSession Session { get; set; }
        public IBus Bus { get; set; }

        public void Handle(EndpointHeartbeat message)
        {
            var originatingEndpoint = EndpointDetails.SendingEndpoint(Bus.CurrentMessageContext.Headers);
            var id = DeterministicGuid.MakeId(originatingEndpoint.Name, originatingEndpoint.Machine);
            var heartbeat = Session.Load<Heartbeat>(id);
            var isNew = false;

            if (heartbeat == null)
            {
                isNew = true;
                heartbeat = new Heartbeat
                {
                    Id = id,
                    ReportedStatus = Status.Beating,
                };
            }

            if (message.ExecutedAt <= heartbeat.LastReportAt)
            {
                Logger.InfoFormat("Out of sync heartbeat received for endpoint {0}", originatingEndpoint.Name);
                return;
            }

            heartbeat.LastReportAt = message.ExecutedAt;
            heartbeat.OriginatingEndpoint = originatingEndpoint;

            if (isNew) // New endpoint heartbeat
            {
                Bus.Publish(new HeartbeatingEndpointDetected
                {
                    Endpoint = heartbeat.OriginatingEndpoint.Name,
                    Machine = heartbeat.OriginatingEndpoint.Machine,
                    DetectedAt = heartbeat.LastReportAt,
                });
            }

            if (heartbeat.ReportedStatus == Status.Dead)
            {
                heartbeat.ReportedStatus = Status.Beating;
                Bus.Publish(new EndpointHeartbeatRestored
                {
                    Endpoint = heartbeat.OriginatingEndpoint.Name,
                    Machine = heartbeat.OriginatingEndpoint.Machine,
                    RestoredAt = heartbeat.LastReportAt
                });
            }

            Session.Store(heartbeat);
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(SaveHeartbeatHandler));
    }
}