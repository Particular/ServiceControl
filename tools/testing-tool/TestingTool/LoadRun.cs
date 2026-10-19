using System.Diagnostics;

namespace TestingTool;

/// <summary>
/// State for one direct-write run: target rate, shared send budget, cancellation and workers.
/// Shared by the error and audit queue writers so both pace identically.
/// </summary>
internal class LoadRun
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _claimed;
    private long _written;

    protected LoadRun(double rate, int workerCount, TimeSpan? duration)
    {
        Rate = rate;
        WorkerCount = workerCount;
        StartedAt = DateTimeOffset.UtcNow;
        Cts = duration is { } d
            ? new CancellationTokenSource(d)
            : new CancellationTokenSource();
    }

    public double Rate { get; }
    public int WorkerCount { get; }
    public DateTimeOffset StartedAt { get; }
    public CancellationTokenSource Cts { get; }
    public Task[]? Loops { get; set; }
    public long Written => Interlocked.Read(ref _written);

    /// <summary>Envelopes actually written per second since start, for comparison with <see cref="Rate"/>.</summary>
    public double AchievedRate
    {
        get
        {
            var elapsed = _clock.Elapsed.TotalSeconds;
            return elapsed > 0 ? Written / elapsed : 0;
        }
    }

    public void IncrementWritten() => Interlocked.Increment(ref _written);

    /// <summary>
    /// Claims the next sequence number if one is owed (<c>floor(elapsed * rate)</c> not yet
    /// reached). Shared by all workers so whichever is free picks up the backlog.
    /// </summary>
    public bool TryClaim(out long sequence)
    {
        var due = (long)(_clock.Elapsed.TotalSeconds * Rate);
        while (true)
        {
            var claimed = Interlocked.Read(ref _claimed);
            if (claimed >= due)
            {
                sequence = 0;
                return false;
            }
            if (Interlocked.CompareExchange(ref _claimed, claimed + 1, claimed) == claimed)
            {
                sequence = claimed + 1;
                return true;
            }
        }
    }

    /// <summary>
    /// The tick a worker should wait between catch-up passes. The floor keeps PeriodicTimer valid
    /// (it rejects sub-millisecond periods) and avoids spinning; the ceiling keeps low rates responsive.
    /// </summary>
    public TimeSpan WorkerTick => TimeSpan.FromSeconds(Math.Clamp(WorkerCount / Rate, MinTick.TotalSeconds, MaxTick.TotalSeconds));

    private static readonly TimeSpan MinTick = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan MaxTick = TimeSpan.FromSeconds(1);
}
