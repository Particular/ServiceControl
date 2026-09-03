using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TestingTool.Scenarios;

namespace TestingTool.Jobs;

/// <summary>
/// Search job that runs canned full-text-search queries against ServiceControl on a cycle.
/// Exercises the ServiceControl search index under concurrent load and records latency metrics.
/// This is the UI-controllable successor to the old hidden <c>SearchService</c> background timer.
/// </summary>
public sealed class SearchJob(
    ServiceControlClient sc,
    TestingToolMetrics metrics,
    IOptions<TestingToolOptions> options,
    Meter meter,
    ILogger<SearchJob> logger) : JobBase("testing-tool.search")
{
    private readonly Counter<long> _searchCounter = meter.CreateCounter<long>("searches_executed_total");
    private readonly Histogram<double> _searchLatency = meter.CreateHistogram<double>("search_latency_ms", "ms");

    // Terms drawn from the same vocabulary embedded into generated message bodies by
    // MessageTextGenerator, so the queries are guaranteed to match indexed content and
    // genuinely exercise ServiceControl's full-text search index rather than returning empty
    // results. We keep a couple of exception-related terms too: those appear in the failure
    // headers (NServiceBus.ExceptionInfo.Message) that ServiceControl also indexes.
    private static readonly string[] CannedQueries = BuildQueries();

    public override string Name => "search";
    public override string Description => "Run canned full-text-search queries against ServiceControl to exercise the search index.";
    public override string Category => "Search";
    public override TimeSpan DefaultInterval => options.Value.SearchInterval;

    private static string[] BuildQueries()
    {
        // Start with the searchable terms embedded in generated message bodies...
        var queries = new List<string>(MessageTextGenerator.SearchableTerms);

        // ...then add a few terms that appear in scenario exception headers, which ServiceControl
        // also indexes for full-text search.
        queries.AddRange(["exception", "timeout", "poison", "deserialization"]);

        return queries.ToArray();
    }

    protected override async Task ExecuteCycleAsync(CancellationToken ct)
    {
        // Run a few random queries per cycle to exercise the search index.
        var queries = CannedQueries.OrderBy(_ => Random.Shared.Next()).Take(3).ToList();
        foreach (var query in queries)
        {
            ct.ThrowIfCancellationRequested();

            var sw = Stopwatch.StartNew();
            var result = await sc.SearchAsync(query, ct);
            sw.Stop();

            _searchLatency.Record(sw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("query", query));

            AddItems(1);
            metrics.AddSearches(1);
            _searchCounter.Add(1, new KeyValuePair<string, object?>("query", query));

            logger.LogDebug("Search '{Query}' → {Count} results in {Ms:F1}ms",
                query, result?.MessageCount, sw.Elapsed.TotalMilliseconds);
        }
    }

    protected override void LogCycleError(Exception ex) => logger.LogWarning(ex, "Search cycle failed");
}