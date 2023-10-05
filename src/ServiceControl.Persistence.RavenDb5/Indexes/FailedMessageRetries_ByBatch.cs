namespace ServiceControl.Persistence
{
    using System.Linq;
    using Raven.Client.Documents.Indexes;
    using ServiceControl.Recoverability;

    class FailedMessageRetries_ByBatch : AbstractIndexCreationTask<FailedMessageRetry>
    {
        public FailedMessageRetries_ByBatch()
        {
            Map = docs =>

                from doc in docs
                select new
                {
                    doc.RetryBatchId
                };
        }
    }
}