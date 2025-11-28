namespace ServiceControl.Persistence
{
    using System.Threading.Tasks;
    using ServiceControl.MessageFailures;

    public interface IEditFailedMessagesManager : IDataSessionManager
    {
        Task<FailedMessage> GetFailedMessage(string failedMessageId);
        Task<string> GetCurrentEditingRequestId(string failedMessageId);
        Task SetCurrentEditingRequestId(string editingMessageId);
        Task SetFailedMessageAsResolved();
    }
}
