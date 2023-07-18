namespace ServiceControl.Persistence
{
    using System.Threading.Tasks;
    using System;

    public interface IRetryDocumentDataStore
    {
        Task StageRetryByUniqueMessageIds(string batchDocumentId, string requestId, RetryType retryType, string[] messageIds,
            DateTime startTime, DateTime? last = null, string originator = null, string batchName = null,
            string classifier = null);

    }
}