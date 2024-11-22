namespace ServiceControl.Persistence
{
    using System;
    using System.Threading.Tasks;

    public interface IDataSessionManager : IDisposable
    {
        Task SaveChanges();
    }
}