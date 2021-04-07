using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Logging;
using Raven.Client.Embedded;

public class RavenHealthReporter
{
    public async void Start(EmbeddableDocumentStore documentStore, CancellationToken cancellationToken = default)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var report = new StringBuilder();

                var statistics = documentStore.DatabaseCommands.GetStatistics();

                report.AppendLine("Report:");
                report.AppendLine($"CountOfStaleIndexesExcludingDisabledAndAbandoned:  {statistics.CountOfStaleIndexesExcludingDisabledAndAbandoned}");

                var indexes = statistics.Indexes.OrderBy(x => x.Name);

                foreach (var indexStats in indexes)
                {
                    var indexLag = indexStats.IndexingLag.GetValueOrDefault();
                    indexLag = Math.Abs(indexLag);
                    report.AppendLine($"Index [{Truncate(indexStats.Name, 30),-30}] Stale: {statistics.StaleIndexes.Contains(indexStats.Name),-5}, Lag: {indexLag,9:n0}, Valid: {indexStats.IsInvalidIndex,-5}, LastIndexed: {indexStats.LastIndexedTimestamp:u}, LastIndexing: {indexStats.LastIndexingTime:u}, Priority: {indexStats.Priority}");

                    if (indexLag > IndexLagThresholdError)
                    {
                        _log.Error($"Index [{indexStats.Name}] IndexingLag {indexLag:n0} is above {IndexLagThresholdError:n0}, consider starting the instance in maintenance mode to ensure index can recover.");
                    }
                    else if (indexLag > IndexLagThresholdWarning)
                    {
                        _log.Warn($"Index [{indexStats.Name}] IndexingLag {indexLag:n0} is above {IndexLagThresholdWarning:n0}, please start the instance in maintenance mode to ensure index can recover.");
                    }
                }

                report.AppendLine($"Stale index count:  {statistics.StaleIndexes.Length}");
                report.AppendLine($"Index error count:  {statistics.Errors.Length}");
                foreach (var indexError in statistics.Errors)
                {
                    report.AppendLine($"Index [{indexError.IndexName}] error: {indexError.Error} (Action: {indexError.Action},  Doc: {indexError.Document}, At: {indexError.Timestamp})");
                }

                _log.Debug(report.ToString());

                await Task.Delay(_interval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch when (documentStore.WasDisposed)
        {
        }
    }

    static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= maxLength ? value : value.Substring(0, maxLength - 1) + "…";
    }

    const uint IndexLagThresholdWarning = 10000;
    const uint IndexLagThresholdError = 100000;
    static TimeSpan _interval = TimeSpan.FromMinutes(15);
    static ILog _log = LogManager.GetLogger<RavenHealthReporter>();
}
