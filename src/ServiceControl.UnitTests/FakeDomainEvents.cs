namespace ServiceControl.UnitTests.Operations
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ServiceControl.Infrastructure.DomainEvents;

    public class FakeDomainEvents : IDomainEvents
    {
        public Task Raise<T>(T domainEvent) where T : IDomainEvent
        {
            RaisedEvents.Add(domainEvent);
            return Task.FromResult(0);
        }

        public List<object> RaisedEvents { get;  } = new List<object>();
    }
}