namespace ServiceControl
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Indexes;
    using Raven.Client.Documents.Operations.Indexes;
    using ServiceControl.Audit.Persistence;

    class CheckRavenDBIndexLag : CustomCheck
    {
        public CheckRavenDBIndexLag(IDocumentStore store)
            : base("Audit Database Index Lag", "ServiceControl.Audit Health", TimeSpan.FromMinutes(5))
        {
            _store = store;
            // TODO verify that this is really not needed anymore
            //LogPath = logPath;
        }

        public override async Task<CheckResult> PerformCheck()
        {
            var statistics = await _store.Maintenance.SendAsync(new GetIndexesStatisticsOperation())
                .ConfigureAwait(false);
            var indexes = statistics.OrderBy(x => x.Name).ToArray();

            CreateDiagnosticsLogEntry(indexes);

            var indexCountWithTooMuchLag = CheckAndReportIndexesWithTooMuchIndexLag(indexes);

            if (indexCountWithTooMuchLag > 0)
            {
                return CheckResult.Failed($"At least one index significantly stale. Please run maintenance mode if this custom check persists to ensure index(es) can recover. See log file in `TODO` for more details. Visit https://docs.particular.net/search?q=servicecontrol+troubleshooting for more information.");
            }

            return CheckResult.Pass;
        }

        static int CheckAndReportIndexesWithTooMuchIndexLag(IndexStats[] indexes)
        {
            int indexCountWithTooMuchLag = 0;

            foreach (var indexStats in indexes)
            {
                // IndexingLag is the number of documents that the index is behind, it is not a time unit.
                // TODO: Should this be MAX instead of a Sum?
                var indexLag = indexStats.Collections.Values.Sum(x => x.DocumentLag);
                indexLag = Math.Abs(indexLag);

                if (indexLag > IndexLagThresholdError)
                {
                    indexCountWithTooMuchLag++;
                    _log.Error($"Index [{indexStats.Name}] IndexingLag {indexLag:n0} is above error threshold ({IndexLagThresholdError:n0}). Launch in maintenance mode to let indexes catch up.");
                }
                else if (indexLag > IndexLagThresholdWarning)
                {
                    indexCountWithTooMuchLag++;
                    _log.Warn($"Index [{indexStats.Name}] IndexingLag {indexLag:n0} is above warning threshold ({IndexLagThresholdWarning:n0}). Launch in maintenance mode to let indexes catch up.");
                }
            }

            return indexCountWithTooMuchLag;
        }

        static void CreateDiagnosticsLogEntry(IndexStats[] indexes)
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
                // TODO: Should this be MAX instead of a Sum?
                var indexLag = indexStats.Collections.Values.Sum(x => x.DocumentLag);
                indexLag = Math.Abs(indexLag);
                report.AppendLine($"- Index [{indexStats.Name,-44}] Stale: {indexStats.IsStale,-5}, Lag: {indexLag,9:n0}, Valid: {indexStats.IsInvalidIndex,-5}, LastIndexing: {indexStats.LastIndexingTime:u}");
            }
            _log.Debug(report.ToString());
        }

        const int IndexLagThresholdWarning = 10000;
        const int IndexLagThresholdError = 100000;
        static ILog _log = LogManager.GetLogger<CheckRavenDBIndexLag>();
        IDocumentStore _store;
        //string LogPath;
    }
}