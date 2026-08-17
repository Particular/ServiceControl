namespace ServiceControl.Persistence
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IEditFailedMessagesDataStore
    {
        Task<IEditFailedMessagesManager> CreateEditFailedMessageManager(CancellationToken cancellationToken = default);
    }
}
