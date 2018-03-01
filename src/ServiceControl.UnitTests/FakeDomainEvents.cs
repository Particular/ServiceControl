namespace ServiceControl.UnitTests.Operations
{
    using NServiceBus;
    using ServiceControl.Infrastructure.DomainEvents;

    public class FakeDomainEvents : IDomainEvents
    {
        public void Raise<T>(T domainEvent) where T : IEvent
        {
        }
    }
}