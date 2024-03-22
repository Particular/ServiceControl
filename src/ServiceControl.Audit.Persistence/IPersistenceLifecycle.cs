namespace ServiceControl.Audit.Persistence
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IPersistenceLifecycle
    {
        Task Initialize(CancellationToken cancellationToken = default);
    }
}