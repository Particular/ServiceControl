namespace ServiceControl.Audit.Persistence.RavenDB.CustomChecks
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using NServiceBus.CustomChecks;
    using Raven.Client.Documents.Operations;
    using ServiceControl.Audit.Persistence.RavenDB;

    class CheckRavenDBIndexLag(IRavenDocumentStoreProvider documentStoreProvider, ILogger<CheckRavenDBIndexLag> logger) : CustomCheck("Audit Database Index Lag", "ServiceControl.Audit Health", TimeSpan.FromMinutes(5))
    {
        public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
        {
            var store = await documentStoreProvider.GetDocumentStore(cancellationToken);
            var statistics = await store.Maintenance.SendAsync(new GetStatisticsOperation(), cancellationToken);
            var indexes = statistics.Indexes.OrderBy(x => x.Name).ToArray();

            CreateDiagnosticsLogEntry(statistics, indexes);

            var indexCountWithTooMuchLag = CheckAndReportIndexesWithTooMuchIndexLag(indexes);

            if (indexCountWithTooMuchLag > 0)
            {
                return CheckResult.Failed($"At least one index is significantly stale. If this custom check persists, pause ingestion so the index(es) can recover: start the instance in maintenance mode for an embedded database, or stop the instance for an external RavenDB server. See log file for more details. Visit https://docs.particular.net/search?q=servicecontrol+troubleshooting for more information.");
            }

            return CheckResult.Pass;
        }

        int CheckAndReportIndexesWithTooMuchIndexLag(IndexInformation[] indexes)
        {
            int indexCountWithTooMuchLag = 0;

            foreach (var indexStats in indexes)
            {
                if (indexStats.IsStale && indexStats.LastIndexingTime.HasValue)
                {
                    var indexLag = DateTime.UtcNow - indexStats.LastIndexingTime.Value;

                    if (indexLag > IndexLagThresholdError)
                    {
                        indexCountWithTooMuchLag++;
                        logger.LogError("Index [{IndexStatsName}] IndexingLag {IndexLag} is above error threshold ({IndexLagThresholdError}). Pause ingestion to let indexes catch up: use maintenance mode for an embedded database, or stop the instance for an external RavenDB server", indexStats.Name, indexLag, IndexLagThresholdError);
                    }
                    else if (indexLag > IndexLagThresholdWarning)
                    {
                        indexCountWithTooMuchLag++;
                        logger.LogWarning("Index [{IndexStatsName}] IndexingLag {IndexLag} is above warning threshold ({IndexLagThresholdWarning}). Pause ingestion to let indexes catch up: use maintenance mode for an embedded database, or stop the instance for an external RavenDB server", indexStats.Name, indexLag, IndexLagThresholdWarning);
                    }
                }
            }

            return indexCountWithTooMuchLag;
        }

        void CreateDiagnosticsLogEntry(DatabaseStatistics statistics, IndexInformation[] indexes)
        {
            if (!logger.IsEnabled(LogLevel.Debug))
            {
                return;
            }

            var report = new StringBuilder();
            report.AppendLine("Internal RavenDB index health report:");
            report.AppendLine($"- DB Size: {statistics.SizeOnDisk.HumaneSize}");
            report.AppendLine($"- LastIndexingTime {statistics.LastIndexingTime:u}");

            foreach (var indexStats in indexes)
            {
                report.AppendLine($"- Index [{indexStats.Name,-44}] State: {indexStats.State}, Stale: {indexStats.IsStale,-5}, Priority: {indexStats.Priority,-6}, LastIndexingTime: {indexStats.LastIndexingTime:u}");
            }

            logger.LogDebug(report.ToString());
        }

        static readonly TimeSpan IndexLagThresholdWarning = TimeSpan.FromMinutes(1);
        static readonly TimeSpan IndexLagThresholdError = TimeSpan.FromMinutes(10);
    }
}