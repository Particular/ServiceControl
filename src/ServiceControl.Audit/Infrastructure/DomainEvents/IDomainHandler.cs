namespace ServiceControl.Audit.Infrastructure.DomainEvents
{
    using System.Threading.Tasks;

    interface IDomainHandler<in T> where T : IDomainEvent
    {
        Task Handle(T domainEvent);
    }
}