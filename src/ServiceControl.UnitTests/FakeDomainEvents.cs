namespace ServiceControl.UnitTests.Operations
{
    using System.Collections.Generic;
    using ServiceControl.Infrastructure.DomainEvents;

    public class FakeDomainEvents : IDomainEvents
    {
        public void Raise<T>(T domainEvent) where T : IDomainEvent
        {
            RaisedEvents.Add(domainEvent);
        }

        public List<object> RaisedEvents { get;  } = new List<object>();
    }
}