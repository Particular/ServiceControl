namespace ServiceControl.HeartbeatMonitoring
{
    using System;
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
            if (string.IsNullOrEmpty(message.EndpointName))
                throw new ArgumentException("Received an EndpointHeartbeat message without proper initialization of the EndpointName in the schema", "message.EndpointName");
            if (string.IsNullOrEmpty(message.Host))
                throw new ArgumentException("Received an EndpointHeartbeat message without proper initialization of the Host in the schema", "message.Host");
            if (message.HostId == Guid.Empty)
                throw new ArgumentException("Received an EndpointHeartbeat message without proper initialization of the HostId in the schema", "message.HostId");

            var id = DeterministicGuid.MakeId(message.EndpointName, message.HostId.ToString());

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
                    ReportedStatus = Status.Beating
                };
            }

            if (message.ExecutedAt <= heartbeat.LastReportAt)
            {
                Logger.InfoFormat("Out of sync heartbeat received for endpoint {0}", message.EndpointName);
                return;
            }

            heartbeat.LastReportAt = message.ExecutedAt;
            heartbeat.EndpointDetails = new EndpointDetails
            {
                HostId = message.HostId,
                Host = message.Host,
                Name = message.EndpointName
            };

            if (isNew) // New endpoint heartbeat
            {
                Bus.Publish(new HeartbeatingEndpointDetected
                {
                    Endpoint = heartbeat.EndpointDetails,
                    DetectedAt = heartbeat.LastReportAt,
                });             
            }

            if (heartbeat.ReportedStatus == Status.Dead)
            {
                heartbeat.ReportedStatus = Status.Beating;
                Bus.Publish(new EndpointHeartbeatRestored
                {
                    Endpoint = heartbeat.EndpointDetails,
                    RestoredAt = heartbeat.LastReportAt
                });
            }

            Session.Store(heartbeat);
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(SaveHeartbeatHandler));
    }
}