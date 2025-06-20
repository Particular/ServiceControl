namespace ServiceControl.Persistence.RavenDB.CustomChecks
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using NServiceBus.CustomChecks;
    using Raven.Client.Documents.Operations;
    using ServiceControl.Persistence.RavenDB;
    using CustomCheck = NServiceBus.CustomChecks.CustomCheck;

    class CheckRavenDBIndexLag(IRavenDocumentStoreProvider documentStoreProvider, ILogger<CheckRavenDBIndexLag> logger) : CustomCheck("Error Database Index Lag", "ServiceControl Health", TimeSpan.FromMinutes(5))
    {
        public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
        {
            var documentStore = await documentStoreProvider.GetDocumentStore(cancellationToken);
            var statistics = await documentStore.Maintenance.SendAsync(new GetStatisticsOperation(), cancellationToken);
            var indexes = statistics.Indexes.OrderBy(x => x.Name).ToArray();

            CreateDiagnosticsLogEntry(statistics, indexes);

            var indexCountWithTooMuchLag = CheckAndReportIndexesWithTooMuchIndexLag(indexes);

            if (indexCountWithTooMuchLag > 0)
            {
                return CheckResult.Failed($"At least one index significantly stale. Please run maintenance mode if this custom check persists to ensure index(es) can recover. See log file for more details. Visit https://docs.particular.net/search?q=servicecontrol+troubleshooting for more information.");
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
                        logger.LogError("Index [{IndexName}] IndexingLag {IndexLag} is above error threshold ({IndexLagThresholdError}). Launch in maintenance mode to let indexes catch up.", indexStats.Name, indexLag, IndexLagThresholdError);
                    }
                    else if (indexLag > IndexLagThresholdWarning)
                    {
                        indexCountWithTooMuchLag++;
                        logger.LogWarning("Index [{IndexName}] IndexingLag {IndexLag} is above warning threshold ({IndexLagThresholdWarning}). Launch in maintenance mode to let indexes catch up.", indexStats.Name, indexLag, IndexLagThresholdWarning);
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