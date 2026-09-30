namespace Particular.LicensingComponent;

using AuditThroughput;
using Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Persistence;
using ServiceControl.Infrastructure.Ingestion;

/// <summary>
/// Polls the ingestion counters every hour, this instance's in process and every audit instance's
/// over its environment endpoint, and folds the deltas into daily records in the licensing store.
/// A counter set whose process start time moved belongs to a restarted process, so its whole value
/// is the delta; one seen for the first time only establishes a baseline, because its totals span
/// an unknown stretch of time.
/// </summary>
public class IngestionHistoryCollectorHostedService(
    ILogger<IngestionHistoryCollectorHostedService> logger,
    ILicensingDataStore dataStore,
    IAuditQuery auditQuery,
    TimeProvider timeProvider,
    IErrorIngestionSnapshotProvider? errorSnapshotProvider = null) : BackgroundService
{
    public TimeSpan DelayStart { get; set; } = TimeSpan.FromSeconds(50);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting {ServiceName}", nameof(IngestionHistoryCollectorHostedService));

        try
        {
            await Task.Delay(DelayStart, cancellationToken);

            using PeriodicTimer timer = new(TimeSpan.FromHours(1), timeProvider);

            do
            {
                try
                {
                    await Collect(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to collect ingestion counters");
                }
            } while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Stopping {ServiceName}", nameof(IngestionHistoryCollectorHostedService));
        }
    }

    internal async Task Collect(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var changed = false;

        if (errorSnapshotProvider is not null)
        {
            changed |= Accumulate(IngestionHistory.ErrorSource, now, [new AuditIngestionSnapshot(LocalKey, errorSnapshotProvider.GetSnapshot())]);
        }

        var auditSnapshots = await auditQuery.GetAuditIngestionSnapshots(cancellationToken);

        if (auditSnapshots.Count > 0)
        {
            changed |= Accumulate(IngestionHistory.AuditSource, now, auditSnapshots);
        }

        if (changed)
        {
            await Save(now, cancellationToken);
        }
    }

    bool Accumulate(string source, DateTime now, List<AuditIngestionSnapshot> snapshots)
    {
        if (!states.TryGetValue(source, out var state))
        {
            states[source] = state = new SourceState();
        }

        if (state.Date != now.Date)
        {
            state.Reset(now.Date);
        }

        var any = false;

        foreach (var (apiUri, snapshot) in snapshots)
        {
            if (state.Baselines.TryGetValue(apiUri, out var last))
            {
                var delta = snapshot.ProcessStartUtc != last.ProcessStartUtc ? snapshot : Diff(snapshot, last);

                state.HourMessages[now.Hour] += delta.Messages;
                state.HourBusySeconds[now.Hour] += delta.BusySeconds;
                state.HourStorageSeconds[now.Hour] += delta.StorageSeconds;
                state.LagOverOneMinute += delta.LagOverOneMinuteMessages;
                state.LagOverTenMinutes += delta.LagOverTenMinutesMessages;
                state.LagOverSixtyMinutes += delta.LagOverSixtyMinutesMessages;
                state.LagKnown += delta.LagKnownMessages;
                any = true;
            }

            state.Baselines[apiUri] = snapshot;
        }

        return any;
    }

    static IngestionCountersSnapshot Diff(IngestionCountersSnapshot current, IngestionCountersSnapshot last) => current with
    {
        Messages = Math.Max(0, current.Messages - last.Messages),
        BusySeconds = Math.Max(0, current.BusySeconds - last.BusySeconds),
        StorageSeconds = Math.Max(0, current.StorageSeconds - last.StorageSeconds),
        LagOverOneMinuteMessages = Math.Max(0, current.LagOverOneMinuteMessages - last.LagOverOneMinuteMessages),
        LagOverTenMinutesMessages = Math.Max(0, current.LagOverTenMinutesMessages - last.LagOverTenMinutesMessages),
        LagOverSixtyMinutesMessages = Math.Max(0, current.LagOverSixtyMinutesMessages - last.LagOverSixtyMinutesMessages),
        LagKnownMessages = Math.Max(0, current.LagKnownMessages - last.LagKnownMessages)
    };

    async Task Save(DateTime now, CancellationToken cancellationToken)
    {
        var history = await dataStore.GetIngestionHistory(cancellationToken) ?? new IngestionHistory([]);

        foreach (var (source, state) in states)
        {
            if (state.Date != now.Date)
            {
                continue;
            }

            // A record already stored for today survived a collector restart, so today's totals
            // build on it rather than overwriting what the previous incarnation had accumulated.
            state.Base ??= history.Days.FirstOrDefault(day => day.Date == state.Date && day.Source == source)
                ?? new IngestionDay(state.Date, source, 0, 0, 0, 0, 0, 0, 0, 0);

            history.Days.RemoveAll(day => day.Date == state.Date && day.Source == source);
            history.Days.Add(state.ToDay(source));
        }

        history.Days.RemoveAll(day => day.Date < now.Date.AddDays(-HistoryDays));

        await dataStore.SaveIngestionHistory(history, cancellationToken);
    }

    class SourceState
    {
        public DateTime Date { get; private set; } = DateTime.MinValue;
        public long[] HourMessages { get; } = new long[24];
        public double[] HourBusySeconds { get; } = new double[24];
        public double[] HourStorageSeconds { get; } = new double[24];
        public long LagOverOneMinute { get; set; }
        public long LagOverTenMinutes { get; set; }
        public long LagOverSixtyMinutes { get; set; }
        public long LagKnown { get; set; }
        public Dictionary<string, IngestionCountersSnapshot> Baselines { get; } = [];
        public IngestionDay? Base { get; set; }

        public void Reset(DateTime date)
        {
            Date = date;
            Array.Clear(HourMessages);
            Array.Clear(HourBusySeconds);
            Array.Clear(HourStorageSeconds);
            LagOverOneMinute = 0;
            LagOverTenMinutes = 0;
            LagOverSixtyMinutes = 0;
            LagKnown = 0;
            Base = null;
        }

        public IngestionDay ToDay(string source)
        {
            var peakHour = 0;

            for (var hour = 1; hour < 24; hour++)
            {
                if (HourMessages[hour] > HourMessages[peakHour])
                {
                    peakHour = hour;
                }
            }

            var peakMessages = HourMessages[peakHour];
            var basePeakWins = Base!.PeakHourMessages >= peakMessages;

            return new IngestionDay(
                Date,
                source,
                Base.Messages + HourMessages.Sum(),
                basePeakWins ? Base.PeakHourMessages : peakMessages,
                basePeakWins ? Base.PeakHourBusySeconds : HourBusySeconds[peakHour],
                basePeakWins ? Base.PeakHourStorageSeconds : HourStorageSeconds[peakHour],
                Base.LagOverOneMinuteMessages + LagOverOneMinute,
                Base.LagOverTenMinutesMessages + LagOverTenMinutes,
                Base.LagOverSixtyMinutesMessages + LagOverSixtyMinutes,
                Base.LagKnownMessages + LagKnown);
        }
    }

    readonly Dictionary<string, SourceState> states = [];

    const string LocalKey = "local";
    const int HistoryDays = 400;
}
