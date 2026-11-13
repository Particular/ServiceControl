namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Raven.Client;
    using Raven.Client.Documents.Operations;
    using Raven.Client.Documents.Queries;

    class RavenAuditRetentionApplier(IRavenDocumentStoreProvider documentStoreProvider, ILogger<RavenAuditRetentionApplier> logger) : IAuditRetentionApplier
    {
        // Ingestion sets the expiry from the ingestion time. That time is not stored; ProcessedAt is the stored time closest to it.
        static readonly string[] Collections = ["ProcessedMessages", "SagaSnapshots"];

        public async Task<long> ApplyRetention(TimeSpan retentionPeriod, IProgress<long> progress, CancellationToken cancellationToken = default)
        {
            var documentStore = await documentStoreProvider.GetDocumentStore(cancellationToken);
            long updated = 0;

            foreach (var collection in Collections)
            {
                var query = new IndexQuery
                {
                    Query = $$"""
                        from {{collection}}
                        update
                        {
                            this['@metadata']['@expires'] = new Date(new Date(this.ProcessedAt).getTime() + $retentionMilliseconds).toISOString();
                        }
                        """,
                    QueryParameters = new Parameters
                    {
                        { "retentionMilliseconds", retentionPeriod.TotalMilliseconds }
                    }
                };

                var patch = new PatchByQueryOperation(query, new QueryOperationOptions { AllowStale = true });
                var operation = await documentStore.Operations.SendAsync(patch, token: cancellationToken);

                var updatedBefore = updated;
                operation.OnProgressChanged += (_, operationProgress) =>
                {
                    if (operationProgress is DeterminateProgress determinate)
                    {
                        progress.Report(updatedBefore + determinate.Processed);
                    }
                };

                var result = await operation.WaitForCompletionAsync<BulkOperationResult>(cancellationToken);
                updated += result.Total;

                logger.LogInformation("Updated the expiry of {Count:N0} {Collection} documents", result.Total, collection);
            }

            return updated;
        }
    }
}
