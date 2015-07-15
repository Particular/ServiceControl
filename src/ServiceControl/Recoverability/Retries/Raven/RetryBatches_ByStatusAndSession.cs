namespace ServiceControl.Recoverability
{
    using System.Linq;
    using Raven.Client.Indexes;

    public class RetryBatches_ByStatusAndSession : AbstractIndexCreationTask<RetryBatch>
    {
        public RetryBatches_ByStatusAndSession()
        {
            Map = docs => from doc in docs
                select new
                {
                    doc.RetrySessionId,
                    doc.Status
                };
        }
    }
}