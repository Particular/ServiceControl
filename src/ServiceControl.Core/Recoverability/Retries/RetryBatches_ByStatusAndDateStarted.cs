namespace ServiceControl.Recoverability.Retries
{
    using System.Linq;
    using Raven.Client.Indexes;

    public class RetryBatches_ByStatusAndDateStarted : AbstractIndexCreationTask<RetryBatch>
    {
        public RetryBatches_ByStatusAndDateStarted()
        {
            Map = docs => from d in docs
                select new
                {
                    d.Status,
                    d.Started
                };

        }
    }
}
