namespace ServiceControl.Persistence
{
    using System.Threading.Tasks;

    public interface IEditFailedMessagesDataStore
    {
        Task<IEditFailedMessagesManager> CreateEditFailedMessageManager();
    }
}
