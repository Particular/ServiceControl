namespace ServiceControl.Recoverability
{
    using System.Linq;
    using Raven.Client.Indexes;

    public class RetryBatches_ByStatus_ReduceInitialBatchSize : AbstractIndexCreationTask<RetryBatch, RetryBatchGroup>
    {
        public RetryBatches_ByStatus_ReduceInitialBatchSize()
        {
            Map = docs => from doc in docs
                select new
                {
                    doc.RequestId,
                    doc.RetryType,
                    HasStagingBatches = doc.Status == RetryBatchStatus.Staging,
                    HasForwardingBatches = doc.Status == RetryBatchStatus.Forwarding,
                    doc.InitialBatchSize,
                    doc.Originator                              
                };

            Reduce = results => from result in results
                group result by new
                {
                    result.RequestId,
                    result.RetryType
                }  into g
                select new 
                {
                    g.Key.RequestId,
                    g.Key.RetryType,
                    g.First().Originator,
                    HasStagingBatches = g.Any(x => x.HasStagingBatches),
                    HasForwardingBatches = g.Any(x => x.HasForwardingBatches),
                    InitialBatchSize = g.Sum(x => x.InitialBatchSize)
                };
            
            DisableInMemoryIndexing = true;
        }
    }
}