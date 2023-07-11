namespace ServiceControl.Persistence
{
    using System;
    using System.Threading.Tasks;
    using ServiceControl.MessageFailures;

    public abstract class AbstractEditFailedMessagesManager : IDataStoreTransaction
    {
        public FailedMessage FailedMessage { get; }

        public AbstractEditFailedMessagesManager(FailedMessage failedMessage)
        {
            FailedMessage = failedMessage;
        }

        public abstract Task<string> GetCurrentEditingMessageId();
        public abstract Task SetCurrentEditingMessageId(string editingMessageId);
        public abstract Task SetFailedMessageAsResolved();

        public abstract Task SaveChanges();

        public virtual void Dispose()
        {
        }
    }

    public interface IDataStoreTransaction : IDisposable
    {
        Task SaveChanges();
    }
}
