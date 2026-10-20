namespace ServiceControl.Infrastructure.Ingestion;

using System;
using System.Threading;

public class IngestionCounters(TimeProvider timeProvider)
{
    public DateTime ProcessStartUtc { get; } = timeProvider.GetUtcNow().UtcDateTime;

    public void RecordBatch(int completedSize, TimeSpan duration)
    {
        if (completedSize > 0)
        {
            Interlocked.Add(ref messages, completedSize);
        }

        Interlocked.Add(ref busyTicks, duration.Ticks);
    }

    public void RecordStorage(TimeSpan duration) => Interlocked.Add(ref storageTicks, duration.Ticks);

    public void RecordLag(TimeSpan lag)
    {
        Interlocked.Increment(ref lagKnown);

        if (lag > TimeSpan.FromMinutes(1))
        {
            Interlocked.Increment(ref lagOverOneMinute);
        }

        if (lag > TimeSpan.FromMinutes(10))
        {
            Interlocked.Increment(ref lagOverTenMinutes);
        }

        if (lag > TimeSpan.FromMinutes(60))
        {
            Interlocked.Increment(ref lagOverSixtyMinutes);
        }
    }

    public IngestionCountersSnapshot GetSnapshot() => new(
        ProcessStartUtc,
        Interlocked.Read(ref messages),
        TimeSpan.FromTicks(Interlocked.Read(ref busyTicks)).TotalSeconds,
        TimeSpan.FromTicks(Interlocked.Read(ref storageTicks)).TotalSeconds,
        Interlocked.Read(ref lagOverOneMinute),
        Interlocked.Read(ref lagOverTenMinutes),
        Interlocked.Read(ref lagOverSixtyMinutes),
        Interlocked.Read(ref lagKnown));

    long messages;
    long busyTicks;
    long storageTicks;
    long lagOverOneMinute;
    long lagOverTenMinutes;
    long lagOverSixtyMinutes;
    long lagKnown;
}

public record IngestionCountersSnapshot(
    DateTime ProcessStartUtc,
    long Messages,
    double BusySeconds,
    double StorageSeconds,
    long LagOverOneMinuteMessages,
    long LagOverTenMinutesMessages,
    long LagOverSixtyMinutesMessages,
    long LagKnownMessages);
