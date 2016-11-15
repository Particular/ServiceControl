namespace ServiceControl.Recoverability
{
    using System;
    using System.Linq;
    using Raven.Client.Indexes;

    public class RetryBatches_ByOperation : AbstractIndexCreationTask<RetryBatch, RetryBatchGroup>
    {
        public RetryBatches_ByOperation()
        {
            Map = docs => from doc in docs
                select new
                {
                    doc.RequestId,
                    doc.RetryType,
                    HasStagingBatches = doc.Status == RetryBatchStatus.Staging,
                    HasForwardingBatches = doc.Status == RetryBatchStatus.Forwarding,
                    doc.InitialBatchSize,
                    doc.Originator,
   				    doc.StartTime,
                    LastModified = MetadataFor(doc).Value<DateTime>("Last-Modified"),
                    FirstBatchId = doc.Id
                };

            Reduce = results => from result in results
                group result by new
                {
                    result.RequestId,
                    result.RetryType
                }  into g
                let lastModified = g.Min(x => x.LastModified)
                select new 
                {
                    g.Key.RequestId,
                    g.Key.RetryType,
                    g.First().Originator,
                    HasStagingBatches = g.Any(x => x.HasStagingBatches),
                    HasForwardingBatches = g.Any(x => x.HasForwardingBatches),
                    InitialBatchSize = g.Sum(x => x.InitialBatchSize),
                    g.First().StartTime,
                    LastModified = lastModified,
                    g.First(x => x.LastModified == lastModified).FirstBatchId 
                };
            
            DisableInMemoryIndexing = true;
        }
    }
}