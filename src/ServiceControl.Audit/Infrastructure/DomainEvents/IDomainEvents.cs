namespace ServiceControl.Audit.Infrastructure.DomainEvents
{
    using System.Threading.Tasks;

    interface IDomainEvents
    {
        Task Raise<T>(T domainEvent) where T : IDomainEvent;
    }
}