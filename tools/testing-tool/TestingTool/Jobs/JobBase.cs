using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TestingTool.Jobs;

/// <summary>
/// Base class for UI-controllable periodic jobs (retry, archive, search). Each job runs a cycle
/// on a configurable interval until stopped, recording how many cycles and items it has
/// processed. Unlike the old config-gated <c>BackgroundService</c> timers, jobs are started and
/// stopped on demand from the web UI.
/// </summary>
public abstract class JobBase
{
    private readonly object _lock = new();
    private readonly ActivitySource _activitySource;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private long _cycles;
    private long _itemsProcessed;
    private DateTimeOffset _startedAt;
    private TimeSpan _interval;

    protected JobBase(string activitySourceName)
    {
        _activitySource = new ActivitySource(activitySourceName);
    }

    /// <summary>The stable, url-safe job name used in API paths.</summary>
    public abstract string Name { get; }

    /// <summary>Human-readable description of what the job does each cycle.</summary>
    public abstract string Description { get; }

    /// <summary>UI grouping category (e.g. "Recoverability", "Search").</summary>
    public abstract string Category { get; }

    /// <summary>Default cycle interval when none is supplied on start.</summary>
    public abstract TimeSpan DefaultInterval { get; }

    /// <summary>Whether the job is currently running.</summary>
    public bool IsRunning => _cts is not null;

    /// <summary>Cycles completed since the current run started.</summary>
    public long Cycles => Interlocked.Read(ref _cycles);

    /// <summary>Items processed since the current run started.</summary>
    public long ItemsProcessed => Interlocked.Read(ref _itemsProcessed);

    /// <summary>The interval of the current run, or the default when idle.</summary>
    public TimeSpan Interval => _cts is null ? DefaultInterval : _interval;

    /// <summary>When the current run started (UTC), or null when idle.</summary>
    public DateTimeOffset? StartedAt => _cts is null ? null : _startedAt;

    /// <summary>Starts the job on the given interval. The first cycle runs immediately.</summary>
    public bool TryStart(TimeSpan interval, out string? error)
    {
        lock (_lock)
        {
            if (IsRunning)
            {
                error = $"Job '{Name}' is already running";
                return false;
            }

            if (interval <= TimeSpan.Zero)
            {
                error = "Interval must be greater than 0";
                return false;
            }

            _interval = interval;
            _startedAt = DateTimeOffset.UtcNow;
            _cycles = 0;
            _itemsProcessed = 0;
            _cts = new CancellationTokenSource();
            _loop = Task.Run(() => RunAsync(_cts.Token));
        }

        OnStarted();
        error = null;
        return true;
    }

    /// <summary>Stops the job.</summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (_cts is null)
                return;

            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        OnStopped();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            // Run the first cycle immediately so a Start click has instant effect.
            await RunCycle(ct);

            while (await timer.WaitForNextTickAsync(ct))
            {
                await RunCycle(ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task RunCycle(CancellationToken ct)
    {
        using var activity = _activitySource.StartActivity($"{Name}-cycle");
        try
        {
            await ExecuteCycleAsync(ct);
            Interlocked.Increment(ref _cycles);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            LogCycleError(ex);
        }
    }

    /// <summary>Performs one cycle of work. Increment <see cref="AddItems"/> for each processed item.</summary>
    protected abstract Task ExecuteCycleAsync(CancellationToken ct);

    /// <summary>Logs a cycle failure.</summary>
    protected abstract void LogCycleError(Exception ex);

    /// <summary>Adds to the item-processed counter for the current run.</summary>
    protected void AddItems(long count) => Interlocked.Add(ref _itemsProcessed, count);

    protected virtual void OnStarted() { }
    protected virtual void OnStopped() { }
}