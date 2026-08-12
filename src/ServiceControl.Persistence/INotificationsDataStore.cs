namespace ServiceControl.Persistence
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface INotificationsDataStore
    {
        Task<INotificationsManager> CreateNotificationsManager(CancellationToken cancellationToken = default);
    }
}
