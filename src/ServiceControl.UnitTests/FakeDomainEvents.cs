namespace ServiceControl.UnitTests.Operations
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Infrastructure.DomainEvents;

    class FakeDomainEvents : IDomainEvents
    {
        public List<object> RaisedEvents { get; } = [];

        public Task Raise<T>(T domainEvent, CancellationToken cancellationToken = default) where T : IDomainEvent
        {
            RaisedEvents.Add(domainEvent);
            return Task.CompletedTask;
        }
    }
}