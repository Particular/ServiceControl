namespace ServiceControl.UnitTests.Operations
{
    using ServiceControl.Infrastructure.DomainEvents;

    public class FakeDomainEvents : IDomainEvents
    {
        public void Raise<T>(T domainEvent) where T : IDomainEvent
        {
        }
    }
}