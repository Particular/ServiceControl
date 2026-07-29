namespace ServiceControl.Persistence
{
    using System;
    using System.Threading.Tasks;

    public interface IFailedMessageRetryDataStore
    {
        Task ProcessPendingRetries(DateTime periodFrom, DateTime periodTo, string queueAddress, Func<string, Task> processCallback);
        Task<string[]> GetRetryPendingMessages(DateTime from, DateTime to, string queueAddress);
        Task RemoveFailedMessageRetry(string uniqueMessageId);
        Task<byte[]> GetFailedMessageBody(string uniqueMessageId);
    }
}
