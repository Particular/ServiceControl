namespace ServiceControl.HeartbeatMonitoring
{
    using System;
    using System.Linq;
    using System.Threading;
    using Contracts.HeartbeatMonitoring;
    using NServiceBus;
    using Raven.Client;

    public class RaiseHeartbeatChanges : IWantToRunWhenBusStartsAndStops, 
        IHandleMessages<HeartbeatingEndpointDetected>,
        IHandleMessages<EndpointFailedToHeartbeat>, 
        IHandleMessages<EndpointHeartbeatRestored>
    {
        public IDocumentStore Store { get; set; }

        public RaiseHeartbeatChanges(IBus bus)
        {
            this.bus = bus;
        }

        public void Start()
        {
            // Get the Heartbeat stats when we first start. 
            using (var session = Store.OpenSession())
            {
                numberOfEndpointsDead = session.Query<Heartbeat>().Count(c => c.ReportedStatus == Status.Dead);
                numberOfEndpointsActive = session.Query<Heartbeat>().Count(c => c.ReportedStatus != Status.Dead);
            }
        }

        public void Stop()
        {
        }


        public void Handle(HeartbeatingEndpointDetected message)
        {
            Interlocked.Increment(ref numberOfEndpointsActive);
            bus.Publish(new TotalEndpointsUpdated
            {
                Active = numberOfEndpointsActive,
                Failing = numberOfEndpointsDead,
                LastUpdatedAt = DateTime.UtcNow
                
            });
        }

        public void Handle(EndpointFailedToHeartbeat message)
        {
            Interlocked.Decrement(ref numberOfEndpointsActive);
            Interlocked.Increment(ref numberOfEndpointsDead);
            bus.Publish(new TotalEndpointsUpdated
            {
                Active = numberOfEndpointsActive,
                Failing = numberOfEndpointsDead,
                LastUpdatedAt = DateTime.UtcNow
            });
        }

        public void Handle(EndpointHeartbeatRestored message)
        {
            Interlocked.Increment(ref numberOfEndpointsActive);
            Interlocked.Decrement(ref numberOfEndpointsDead);
            bus.Publish(new TotalEndpointsUpdated
            {
                Active = numberOfEndpointsActive,
                Failing = numberOfEndpointsDead,
                LastUpdatedAt = DateTime.UtcNow
            });
        }

        static int numberOfEndpointsDead;
        static int numberOfEndpointsActive;
        readonly IBus bus;
    }
}