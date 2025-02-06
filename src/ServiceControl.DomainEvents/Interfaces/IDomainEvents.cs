namespace ServiceControl.Infrastructure.DomainEvents
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IDomainEvents
    {
        Task Raise<T>(T domainEvent, CancellationToken cancellationToken = default) where T : IDomainEvent;
    }
}