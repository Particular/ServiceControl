namespace ServiceControl.HeartbeatMonitoring
{
    using System;
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
            var stats = HeartbeatsStats.Retrieve(Store);

            numberOfEndpointsDead = stats.Item1;
            numberOfEndpointsActive = stats.Item2;
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