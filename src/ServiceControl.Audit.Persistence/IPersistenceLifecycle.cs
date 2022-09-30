namespace ServiceControl.Audit.Persistence
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IPersistenceLifecycle
    {
        Task Start(CancellationToken cancellationToken);
        Task Stop(CancellationToken cancellationToken);
    }
}