using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TestingTool.Jobs;

/// <summary>
/// Recoverability job that fetches error groups from ServiceControl and triggers a retry for
/// each group on every cycle. Replayed messages should then succeed (simulating a fix being
/// applied), which exercises ServiceControl's retry pipeline. This is the UI-controllable
/// successor to the old hidden <c>ReplayService</c> background timer.
/// </summary>
public sealed class RetryJob(
    ServiceControlClient sc,
    TestingToolMetrics metrics,
    IOptions<TestingToolOptions> options,
    Meter meter,
    ILogger<RetryJob> logger) : JobBase("testing-tool.retry")
{
    private readonly Counter<long> _replayCounter = meter.CreateCounter<long>("errors_replayed_total");

    public override string Name => "retry";
    public override string Description => "Retry all error groups in ServiceControl on each cycle (replayed messages then succeed).";
    public override string Category => "Recoverability";
    public override TimeSpan DefaultInterval => options.Value.ReplayInterval;

    protected override async Task ExecuteCycleAsync(CancellationToken ct)
    {
        var minGroupSize = options.Value.ReplayMinGroupSize;
        var groups = await sc.GetErrorGroupsAsync(ct);
        if (groups.Count == 0)
        {
            logger.LogDebug("No error groups to retry");
            return;
        }

        foreach (var group in groups)
        {
            ct.ThrowIfCancellationRequested();
            if (group.Count < minGroupSize)
                continue;

            if (await sc.RetryGroupAsync(group.Id, ct))
            {
                AddItems(group.Count);
                metrics.AddErrorsReplayed(group.Count);
                _replayCounter.Add(group.Count, new KeyValuePair<string, object?>("group", group.Title));
                logger.LogInformation("Retried group {Title} ({Count} messages)", group.Title, group.Count);
            }
        }
    }

    protected override void LogCycleError(Exception ex) => logger.LogWarning(ex, "Retry cycle failed");
}