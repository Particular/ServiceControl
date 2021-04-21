namespace ServiceControl
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;
    using Raven.Client;
    using Raven.Abstractions.Data;
    using ServiceBus.Management.Infrastructure.Settings;

    class CheckRavenDBIndexLag : CustomCheck
    {
        public CheckRavenDBIndexLag(IDocumentStore store, LoggingSettings settings)
            : base(nameof(CheckRavenDBIndexLag), "Storage", TimeSpan.FromMinutes(5))
        {
            _store = store;
            LogPath = settings?.LogPath;
        }

        public override Task<CheckResult> PerformCheck()
        {
            var statistics = _store.DatabaseCommands.GetStatistics();
            var indexes = statistics.Indexes.OrderBy(x => x.Name).ToArray();

            CreateDiagnosticsLogEntry(statistics, indexes);

            var indexCountWithTooMuchLag = CheckAndReportIndexesWithTooMuchIndexLag(statistics, indexes);

            if (indexCountWithTooMuchLag > 0)
            {
                return CheckResult.Failed($"At least one index significantly stale. Please run maintenance mode if this custom check persists to ensure index(es) can recover. See log file in `{LogPath}` for more details. Visit https://docs.particular.net/search?q=servicecontrol+troubleshooting for more information.");
            }

            return CheckResult.Pass;
        }

        static int CheckAndReportIndexesWithTooMuchIndexLag(DatabaseStatistics statistics, IndexStats[] indexes)
        {
            int indexCountWithTooMuchLag = 0;

            foreach (var indexStats in indexes)
            {
                // IndexingLag is the number of documents that the index is behind, it is not a time unit.
                var indexLag = indexStats.IndexingLag.GetValueOrDefault();
                indexLag = Math.Abs(indexLag);

                if (indexLag > IndexLagThresholdError)
                {
                    indexCountWithTooMuchLag++;
                    _log.Error($"Index [{indexStats.Name}] IndexingLag {indexLag:n0} is above {IndexLagThresholdError:n0}, consider starting the instance in maintenance mode to ensure index can recover.");
                }
                else if (indexLag > IndexLagThresholdWarning)
                {
                    indexCountWithTooMuchLag++;
                    _log.Warn($"Index [{indexStats.Name}] IndexingLag {indexLag:n0} is above {IndexLagThresholdWarning:n0}, please start the instance in maintenance mode to ensure index can recover.");
                }
            }

            foreach (var indexError in statistics.Errors)
            {
                _log.Error($"Index [{indexError.IndexName}] Error: {indexError.Error} (Action: {indexError.Action},  Doc: {indexError.Document}, At: {indexError.Timestamp})");
            }

            return indexCountWithTooMuchLag;
        }

        static void CreateDiagnosticsLogEntry(DatabaseStatistics statistics, IndexStats[] indexes)
        {
            if (!_log.IsDebugEnabled)
            {
                return;
            }

            var report = new StringBuilder();
            report.AppendLine("Internal RavenDB index health report:");

            foreach (var indexStats in indexes)
            {
                // IndexingLag is the number of documents that the index is behind, it is not a time unit.
                var indexLag = indexStats.IndexingLag.GetValueOrDefault();
                indexLag = Math.Abs(indexLag);
                report.AppendLine($"- Index [{indexStats.Name,-44}] Stale: {statistics.StaleIndexes.Contains(indexStats.Name),-5}, Lag: {indexLag,9:n0}, Valid: {indexStats.IsInvalidIndex,-5}, LastIndexed: {indexStats.LastIndexedTimestamp:u}, LastIndexing: {indexStats.LastIndexingTime:u}");
            }
            _log.Debug(report.ToString());
        }

        const int IndexLagThresholdWarning = 10000;
        const int IndexLagThresholdError = 100000;
        static ILog _log = LogManager.GetLogger<CheckRavenDBIndexLag>();
        IDocumentStore _store;
        string LogPath;
    }
}