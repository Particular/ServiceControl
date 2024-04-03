namespace ServiceControl.Audit.Persistence.RavenDB.CustomChecks
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;
    using Raven.Client.Documents.Operations;
    using ServiceControl.Audit.Persistence.RavenDB;

    class CheckRavenDBIndexLag(IRavenDocumentStoreProvider documentStoreProvider) : CustomCheck("Audit Database Index Lag", "ServiceControl.Audit Health", TimeSpan.FromMinutes(5))
    {
        public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
        {
            var store = documentStoreProvider.GetDocumentStore();
            var statistics = await store.Maintenance.SendAsync(new GetStatisticsOperation(), cancellationToken);
            var indexes = statistics.Indexes.OrderBy(x => x.Name).ToArray();

            CreateDiagnosticsLogEntry(statistics, indexes);

            var indexCountWithTooMuchLag = CheckAndReportIndexesWithTooMuchIndexLag(indexes);

            if (indexCountWithTooMuchLag > 0)
            {
                return CheckResult.Failed($"At least one index significantly stale. Please run maintenance mode if this custom check persists to ensure index(es) can recover. See log file for more details. Visit https://docs.particular.net/search?q=servicecontrol+troubleshooting for more information.");
            }

            return CheckResult.Pass;
        }

        static int CheckAndReportIndexesWithTooMuchIndexLag(IndexInformation[] indexes)
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
                        Log.Error($"Index [{indexStats.Name}] IndexingLag {indexLag} is above error threshold ({IndexLagThresholdError}). Launch in maintenance mode to let indexes catch up.");
                    }
                    else if (indexLag > IndexLagThresholdWarning)
                    {
                        indexCountWithTooMuchLag++;
                        Log.Warn($"Index [{indexStats.Name}] IndexingLag {indexLag} is above warning threshold ({IndexLagThresholdWarning}). Launch in maintenance mode to let indexes catch up.");
                    }
                }
            }

            return indexCountWithTooMuchLag;
        }

        static void CreateDiagnosticsLogEntry(DatabaseStatistics statistics, IndexInformation[] indexes)
        {
            if (!Log.IsDebugEnabled)
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
            Log.Debug(report.ToString());
        }

        static readonly TimeSpan IndexLagThresholdWarning = TimeSpan.FromMinutes(1);
        static readonly TimeSpan IndexLagThresholdError = TimeSpan.FromMinutes(10);
        static readonly ILog Log = LogManager.GetLogger<CheckRavenDBIndexLag>();
    }
}