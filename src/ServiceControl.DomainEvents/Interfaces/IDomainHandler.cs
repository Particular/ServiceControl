namespace ServiceControl.Infrastructure.DomainEvents
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IDomainHandler<in T> where T : IDomainEvent
    {
        Task Handle(T domainEvent, CancellationToken cancellationToken = default);
    }
}