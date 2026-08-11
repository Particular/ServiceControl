namespace ServiceControl.Persistence
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IDataSessionManager : IAsyncDisposable
    {
        Task SaveChanges(CancellationToken cancellationToken = default);
    }
}
