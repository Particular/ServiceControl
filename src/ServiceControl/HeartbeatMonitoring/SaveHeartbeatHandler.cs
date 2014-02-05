namespace ServiceControl.HeartbeatMonitoring
{
    using Contracts.HeartbeatMonitoring;
    using Contracts.Operations;
    using EndpointControl;
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
            Heartbeat heartbeat = null;
            KnownEndpoint knownEndpoint = null;

            Session.Advanced.Lazily.Load<Heartbeat>(id, doc => heartbeat = doc);
            Session.Advanced.Lazily.Load<KnownEndpoint>(id, doc => knownEndpoint = doc);

            Session.Advanced.Eagerly.ExecuteAllPendingLazyOperations();

            if (knownEndpoint != null)
            {
                if (!knownEndpoint.MonitorHeartbeat)
                {
                    return;
                }
            }

            var isNew = false;

            if (heartbeat == null)
            {
                isNew = true;
                heartbeat = new Heartbeat
                {
                    Id = id,
                    ReportedStatus = Status.Beating,
                    KnownEndpointId = "KnownEndpoints/" + id,
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