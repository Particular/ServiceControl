namespace ServiceControl.Persistence
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IFailedMessageRetryDataStore
    {
        Task ProcessPendingRetries(DateTime periodFrom, DateTime periodTo, string queueAddress, Func<string, CancellationToken, Task> processCallback, CancellationToken cancellationToken = default);
        Task<string[]> GetRetryPendingMessages(DateTime from, DateTime to, string queueAddress, CancellationToken cancellationToken = default);
        Task RemoveFailedMessageRetry(string uniqueMessageId, CancellationToken cancellationToken = default);
        Task<byte[]> GetFailedMessageBody(string uniqueMessageId, CancellationToken cancellationToken = default);
    }
}
