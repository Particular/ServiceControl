namespace ServiceControl.Infrastructure.DomainEvents
{
    public interface IDomainHandler<in T> where T : IDomainEvent
    {
        void Handle(T domainEvent);
    }
}