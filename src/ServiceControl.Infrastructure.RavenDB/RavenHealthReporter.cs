using System;
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
                var statistics = documentStore.DatabaseCommands.GetStatistics();
                _log.Info($"CountOfStaleIndexesExcludingDisabledAndAbandoned:  {statistics.CountOfStaleIndexesExcludingDisabledAndAbandoned}");

                foreach (var indexStats in statistics.Indexes)
                {
                    _log.Info($"Index [{Truncate(indexStats.Name, 30),-30}] IndexingLag: {indexStats.IndexingLag,7}, IsInvalidIndex: {indexStats.IsInvalidIndex,-5}, LastIndexedTimestamp:{indexStats.LastIndexedTimestamp:s}, LastIndexingTime:{indexStats.LastIndexingTime:s}, Priority: {indexStats.Priority}");
                }

                _log.Info($"Stale index count:  {statistics.StaleIndexes.Length}");

                foreach (var staleIndexName in statistics.StaleIndexes)
                {
                    _log.Info($"Index [{staleIndexName}] is stale");
                }

                _log.Info($"Index error count:  {statistics.Errors.Length}");
                foreach (var indexError in statistics.Errors)
                {
                    _log.Info($"Index [{indexError.IndexName}] error: {indexError.Error} (Action: {indexError.Action},  Doc: {indexError.Document}, At: {indexError.Timestamp})");
                }
                await Task.Delay(_interval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
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

    static TimeSpan _interval = TimeSpan.FromMinutes(15);
    static ILog _log = LogManager.GetLogger<RavenHealthReporter>();
}