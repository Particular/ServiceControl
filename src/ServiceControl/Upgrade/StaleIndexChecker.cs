namespace Particular.ServiceControl.Upgrade
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::ServiceControl.MessageFailures.Api;
    using Raven.Client;

    public class StaleIndexChecker
    {
        private IDocumentStore documentStore;

        public StaleIndexChecker(IDocumentStore store)
        {
            documentStore = store;
        }

        public virtual async Task<bool> IsReindexingInComplete(DateTime cutOffTime, CancellationToken cancellationToken)
        {
            try
            {
                using(var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                using (var session = documentStore.OpenAsyncSession())
                {
                    cts.CancelAfter(TimeSpan.FromMinutes(1));
                    
                    RavenQueryStatistics stats;
                    await session.Advanced.AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                        .SetResultTransformer(new FailedMessageViewTransformer().TransformerName)
                        .Take(1)
                        .Statistics(out stats)
                        .WaitForNonStaleResultsAsOf(cutOffTime)
                        .SelectFields<FailedMessageView>()
                        .ToListAsync(cts.Token);

                    return !stats.IsStale;
                }
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }
}