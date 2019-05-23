namespace Particular.ServiceControl.Upgrade
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::ServiceControl.MessageFailures.Api;
    using NServiceBus.Logging;
    using Raven.Client;

    public class StaleIndexChecker
    {
        public StaleIndexChecker(IDocumentStore store)
        {
            documentStore = store;
        }

        public virtual async Task<bool> IsReindexingInComplete(DateTime cutOffTime, CancellationToken cancellationToken)
        {
            try
            {
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                using (var session = documentStore.OpenAsyncSession())
                {
                    cts.CancelAfter(TimeSpan.FromMinutes(1));

                    await session.Advanced.AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                        .SetResultTransformer(FailedMessageViewTransformer.Name)
                        .Take(1)
                        .Statistics(out var stats)
                        .WaitForNonStaleResultsAsOf(cutOffTime)
                        .SelectFields<FailedMessageView>()
                        .ToListAsync(cts.Token)
                        .ConfigureAwait(false);

                    return !stats.IsStale;
                }
            }
            catch (OperationCanceledException)
            {
                logger.Debug("Waiting for non-stale indexes timed out");
                return false;
            }
            catch (Exception ex)
            {
                logger.Warn("Waiting for non-stale indexes threw unexpected exception.", ex);
                return false;
            }
        }

        IDocumentStore documentStore;
        static ILog logger = LogManager.GetLogger(typeof(StaleIndexChecker));
    }
}