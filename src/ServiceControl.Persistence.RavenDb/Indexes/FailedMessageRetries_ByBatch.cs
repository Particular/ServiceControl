namespace ServiceControl.Persistence
{
    using System.Linq;
    using Raven.Client.Indexes;
    using ServiceControl.Recoverability;

    public class FailedMessageRetries_ByBatch : AbstractIndexCreationTask<FailedMessageRetry>
    {
        public FailedMessageRetries_ByBatch()
        {
            Map = docs => from doc in docs
                          select new
                          {
                              doc.RetryBatchId
                          };

            DisableInMemoryIndexing = true;
        }
    }
}