namespace ServiceControl.Persistence
{
    using System.Threading.Tasks;

    public interface INotificationsDataStore
    {
        Task<INotificationsManager> CreateNotificationsManager();
    }
}
