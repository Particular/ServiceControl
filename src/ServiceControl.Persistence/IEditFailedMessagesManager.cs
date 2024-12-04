namespace ServiceControl.Persistence
{
    using System.Threading.Tasks;
    using ServiceControl.MessageFailures;

    public interface IEditFailedMessagesManager : IDataSessionManager
    {
        Task<FailedMessage> GetFailedMessage(string failedMessageId);
        Task<string> GetCurrentEditingMessageId(string failedMessageId);
        Task SetCurrentEditingMessageId(string editingMessageId);
        Task SetFailedMessageAsResolved();
    }
}