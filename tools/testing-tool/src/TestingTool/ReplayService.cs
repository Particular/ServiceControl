using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;

namespace TestingTool;

/// <summary>
/// Background service that periodically fetches error groups from ServiceControl and triggers
/// retry/replay. Replayed messages should then succeed (simulating a fix being applied), which
/// exercises ServiceControl's retry pipeline. Gated by configuration so replicas can opt in/out.
/// </summary>
public sealed class ReplayService(
    ServiceControlClient sc,
    TestingToolMetrics metrics,
    IOptions<TestingToolOptions> options,
    Meter meter,
    ILogger<ReplayService> logger) : BackgroundService
{
    private readonly Counter<long> _replayCounter = meter.CreateCounter<long>("errors_replayed_total");
    private readonly ActivitySource _activitySource = new("testing-tool.replay");
    private long _totalReplayed;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.ReplayEnabled)
        {
            logger.LogInformation("Replay job disabled by configuration");
            return;
        }

        var interval = options.Value.ReplayInterval;
        logger.LogInformation("Replay job started — interval {Interval}", interval);

        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var activity = _activitySource.StartActivity("replay-cycle");
                var groups = await sc.GetErrorGroupsAsync(stoppingToken);

                if (groups.Count == 0)
                {
                    logger.LogDebug("No error groups to replay");
                    continue;
                }

                foreach (var group in groups)
                {
                    if (group.Count < options.Value.ReplayMinGroupSize)
                        continue;

                    var success = await sc.ReplayGroupAsync(group.Id, stoppingToken);
                    if (success)
                    {
                        Interlocked.Add(ref _totalReplayed, group.Count);
                        metrics.AddErrorsReplayed(group.Count);
                        _replayCounter.Add(group.Count, new KeyValuePair<string, object?>("group", group.Title));
                        logger.LogInformation("Replayed group {Title} ({Count} messages)", group.Title, group.Count);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Replay cycle failed");
            }
        }
    }

    public long TotalReplayed => Interlocked.Read(ref _totalReplayed);
}