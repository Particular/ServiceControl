namespace ServiceControl.Infrastructure.DomainEvents
{
    public interface IDomainEvents
    {
        void Raise<T>(T domainEvent) where T : IDomainEvent;
    }
}