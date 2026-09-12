using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TestingTool.Jobs;

/// <summary>
/// Recoverability job that fetches error groups from ServiceControl and archives each group on
/// every cycle. This exercises ServiceControl's archive pipeline and retention behaviour, and
/// complements the <see cref="RetryJob"/>. Controllable from the web UI rather than running as a
/// hidden background timer.
/// </summary>
public sealed class ArchiveJob(
    ServiceControlClient sc,
    TestingToolMetrics metrics,
    IOptions<TestingToolOptions> options,
    Meter meter,
    ILogger<ArchiveJob> logger) : JobBase("testing-tool.archive")
{
    private readonly Counter<long> _archiveCounter = meter.CreateCounter<long>("errors_archived_total");

    public override string Name => "archive";
    public override string Description => "Archive all error groups in ServiceControl on each cycle (moves failures to the archive).";
    public override string Category => "Recoverability";
    public override TimeSpan DefaultInterval => options.Value.ArchiveInterval;

    protected override async Task ExecuteCycleAsync(CancellationToken ct)
    {
        var minGroupSize = options.Value.ArchiveMinGroupSize;
        var groups = await sc.GetErrorGroupsAsync(ct);
        if (groups.Count == 0)
        {
            logger.LogDebug("No error groups to archive");
            return;
        }

        foreach (var group in groups)
        {
            ct.ThrowIfCancellationRequested();
            if (group.Count < minGroupSize)
                continue;

            if (await sc.ArchiveGroupAsync(group.Id, ct))
            {
                AddItems(group.Count);
                metrics.AddErrorsArchived(group.Count);
                _archiveCounter.Add(group.Count, new KeyValuePair<string, object?>("group", group.Title));
                logger.LogInformation("Archived group {Title} ({Count} messages)", group.Title, group.Count);
            }
        }
    }

    protected override void LogCycleError(Exception ex) => logger.LogWarning(ex, "Archive cycle failed");
}