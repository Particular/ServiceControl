namespace ServiceControl.Infrastructure.DomainEvents
{
    using System.Threading.Tasks;

    public interface IDomainEvents
    {
        Task Raise<T>(T domainEvent) where T : IDomainEvent;
    }
}