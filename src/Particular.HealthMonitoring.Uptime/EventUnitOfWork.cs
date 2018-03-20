namespace Particular.HealthMonitoring.Uptime
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Particular.HealthMonitoring.Uptime.Api;
    using ServiceControl.Infrastructure.DomainEvents;

    class EventUnitOfWork : IDomainEvents
    {
        IDomainEvents domainEvents;
        IPersistEndpointUptimeInformation persister;
        List<IHeartbeatEvent> recordedEvents = new List<IHeartbeatEvent>();

        public EventUnitOfWork(IDomainEvents domainEvents, IPersistEndpointUptimeInformation persister)
        {
            this.domainEvents = domainEvents;
            this.persister = persister;
        }

        public void Raise<T>(T domainEvent) where T : IDomainEvent
        {
            domainEvents.Raise(domainEvent);
            var heartbeatEvent = domainEvent as IHeartbeatEvent;
            if (heartbeatEvent != null)
            {
                recordedEvents.Add(heartbeatEvent);
            }
        }

        public async Task Persist()
        {
            foreach (var @event in recordedEvents)
            {
                await persister.Store(@event).ConfigureAwait(false);
            }
        }
    }
}