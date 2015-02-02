namespace ServiceControl.HeartbeatMonitoring
{
    using System;
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
        public HeartbeatStatusProvider HeartbeatStatusProvider { get; set; }

        public void Handle(EndpointHeartbeat message)
        {
            if (string.IsNullOrEmpty(message.EndpointName))
            {
                throw new ArgumentException("Received an EndpointHeartbeat message without proper initialization of the EndpointName in the schema", "message.EndpointName");
            }

            if (string.IsNullOrEmpty(message.Host))
            {
                throw new ArgumentException("Received an EndpointHeartbeat message without proper initialization of the Host in the schema", "message.Host");
            }

            if (message.HostId == null)
            {
                throw new ArgumentException("Received an EndpointHeartbeat message without proper initialization of the HostId in the schema", "message.HostId");
            }
                

            var id = DeterministicGuid.MakeId(message.EndpointName, message.HostId.ToString("N"));

            var heartbeat = Session.Load<Heartbeat>(id);
          
            if (heartbeat != null)
            {
                if (heartbeat.Disabled)
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
                Session.Store(heartbeat);
            }

            if (message.ExecutedAt <= heartbeat.LastReportAt)
            {
                Logger.InfoFormat("Out of sync heartbeat received for endpoint {0}", message.EndpointName);
                return;
            }

            heartbeat.LastReportAt = message.ExecutedAt;
            heartbeat.EndpointDetails = new EndpointDetails
            {
                HostId = message.HostId.ToString("N"),
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

            HeartbeatStatusProvider.RegisterHeartbeatingEndpoint(heartbeat.EndpointDetails, heartbeat.LastReportAt);
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(SaveHeartbeatHandler));
    }
}