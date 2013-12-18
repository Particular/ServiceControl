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
        public IDocumentStore Store { get; set; }
        public IBus Bus { get; set; }

        public void Handle(EndpointHeartbeat message)
        {
            using (var session = Store.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var originatingEndpoint = EndpointDetails.SendingEndpoint(Bus.CurrentMessageContext.Headers);
                var id = string.Format("heartbeats/{0}",DeterministicGuid.MakeId(originatingEndpoint.Name, originatingEndpoint.Machine));

                var heartbeat = session.Load<Heartbeat>(id) ?? new Heartbeat
                {
                    Id = id,
                    ReportedStatus = Status.New,
                };

                if (message.ExecutedAt <= heartbeat.LastReportAt)
                {
                    Logger.InfoFormat("Out of sync heartbeat received for endpoint {0}",originatingEndpoint.Name);
                    return;
                }

                heartbeat.LastReportAt = message.ExecutedAt;
                heartbeat.OriginatingEndpoint = originatingEndpoint;
                
                if (heartbeat.ReportedStatus == Status.New) // New endpoint heartbeat
                {
                    Bus.Publish(new HeartbeatingEndpointDetected
                    {
                        Endpoint = heartbeat.OriginatingEndpoint.Name,
                        Machine = heartbeat.OriginatingEndpoint.Machine,
                        DetectedAt = heartbeat.LastReportAt,
                    });
                }

                if (heartbeat.ReportedStatus == Status.Dead) // Was prevo
                {
                    heartbeat.ReportedStatus = Status.Beating;
                    Bus.Publish(new EndpointHeartbeatRestored
                    {
                        Endpoint = heartbeat.OriginatingEndpoint.Name,
                        Machine = heartbeat.OriginatingEndpoint.Machine,
                        RestoredAt = heartbeat.LastReportAt
                    });
                }

                session.Store(heartbeat);
                session.SaveChanges();
            }
        }

        static ILog Logger = LogManager.GetLogger(typeof(SaveHeartbeatHandler));
    }
}