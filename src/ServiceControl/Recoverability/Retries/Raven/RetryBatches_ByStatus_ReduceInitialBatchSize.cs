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
                    doc.Status, 
                    doc.InitialBatchSize                              
                };

            Reduce = results => from result in results
                group result by new
                {
                    result.RequestId,
                    result.RetryType,
                    result.Status
                }  into g
                select new 
                {
                                    
                    g.Key.RequestId,
                    g.Key.RetryType,
                    g.Key.Status,
                    InitialBatchSize = g.Sum(x => x.InitialBatchSize)
                };
            
            DisableInMemoryIndexing = true;
        }
    }
}