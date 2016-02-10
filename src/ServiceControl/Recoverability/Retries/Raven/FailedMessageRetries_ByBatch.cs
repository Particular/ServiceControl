namespace ServiceControl.Recoverability
{
    using System.Linq;
    using Raven.Client.Indexes;

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