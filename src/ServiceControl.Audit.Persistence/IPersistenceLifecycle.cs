namespace ServiceControl.Audit.Persistence
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IPersistenceLifecycle
    {
        Task Start(Action onRavenServerExit, CancellationToken cancellationToken = default);
        Task Stop(CancellationToken cancellationToken = default);
    }
}