namespace ServiceControl.Persistence
{
    using System;
    using System.Threading.Tasks;

    public interface IDataSessionManager : IAsyncDisposable
    {
        Task SaveChanges();
    }
}
