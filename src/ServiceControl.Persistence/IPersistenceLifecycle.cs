namespace ServiceControl.Persistence
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IPersistenceLifecycle // TODO: We don't need this, implementations should just implement IHostedService
    {
        Task Start(CancellationToken cancellationToken = default);
        Task Stop(CancellationToken cancellationToken = default);
    }
}
