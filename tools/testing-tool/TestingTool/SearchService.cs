using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;

namespace TestingTool;

/// <summary>
/// Background service that runs canned full-text-search queries against ServiceControl on a timer.
/// Exercises the RavenDB FTS index under concurrent load and records latency metrics. Gated by
/// configuration so replicas can opt in/out.
/// </summary>
public sealed class SearchService(
    ServiceControlClient sc,
    TestingToolMetrics metrics,
    IOptions<TestingToolOptions> options,
    Meter meter,
    ILogger<SearchService> logger) : BackgroundService
{
    private readonly Counter<long> _searchCounter = meter.CreateCounter<long>("searches_executed_total");
    private readonly Histogram<double> _searchLatency = meter.CreateHistogram<double>("search_latency_ms", "ms");
    private readonly ActivitySource _activitySource = new("testing-tool.search");
    private long _totalSearches;

    // Canned queries that exercise different FTS index paths.
    private static readonly string[] CannedQueries =
    [
        "exception",
        "timeout",
        "NullReferenceException",
        "downstream",
        "deserialization",
        "poison",
        "503",
        "retry"
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.SearchEnabled)
        {
            logger.LogInformation("Search job disabled by configuration");
            return;
        }

        var interval = options.Value.SearchInterval;
        logger.LogInformation("Search job started — interval {Interval}", interval);

        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var activity = _activitySource.StartActivity("search-cycle");

                // Run a few random queries per tick to exercise the FTS index.
                var queries = CannedQueries.OrderBy(_ => Random.Shared.Next()).Take(3).ToList();
                foreach (var query in queries)
                {
                    var sw = Stopwatch.StartNew();
                    var result = await sc.SearchAsync(query, stoppingToken);
                    sw.Stop();

                    _searchLatency.Record(sw.Elapsed.TotalMilliseconds,
                        new KeyValuePair<string, object?>("query", query));

                    Interlocked.Increment(ref _totalSearches);
                    metrics.AddSearches(1);
                    _searchCounter.Add(1, new KeyValuePair<string, object?>("query", query));

                    activity?.SetTag($"search.{query}.count", result?.MessageCount);
                    activity?.SetTag($"search.{query}.latency_ms", sw.Elapsed.TotalMilliseconds);

                    logger.LogDebug("Search '{Query}' → {Count} results in {Ms:F1}ms",
                        query, result?.MessageCount, sw.Elapsed.TotalMilliseconds);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Search cycle failed");
            }
        }
    }

    public long TotalSearches => Interlocked.Read(ref _totalSearches);
}