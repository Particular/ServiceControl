using Microsoft.Extensions.Logging;
using TestingTool.Contracts;

namespace TestingTool.Smoke;

/// <summary>
/// Runs the smoke checklist once at a time, sequentially. A failed check does not stop later checks.
/// The state of the current or last run stays in memory so the UI can reload and show it.
/// </summary>
public sealed class SmokeRunner(
    IReadOnlyList<SmokeCheck> checks,
    Func<string?> findStartConflict,
    TimeSpan defaultCheckTimeout,
    TimeSpan cleanupTimeout,
    TimeSpan pollInterval,
    TimeSpan pollTimeout,
    ILogger<SmokeRunner> logger)
{
    readonly Lock gate = new();
    readonly CheckState[] states = [.. checks.Select(c => new CheckState(c))];
    RunState? run;

    public bool TryStart(out string? error)
    {
        lock (gate)
        {
            if (run is { FinishedAt: null })
            {
                error = "A smoke run is already in progress";
                return false;
            }

            if (findStartConflict() is { } conflict)
            {
                error = conflict;
                return false;
            }

            foreach (var state in states)
            {
                state.Reset();
            }

            var current = new RunState($"smoke-{Guid.NewGuid():N}"[..14]);
            run = current;
            current.Completion = Task.Run(() => ExecuteAsync(current));
        }

        error = null;
        return true;
    }

    public void Stop()
    {
        lock (gate)
        {
            if (run is { FinishedAt: null } current)
            {
                current.Cancellation.Cancel();
            }
        }
    }

    /// <summary>Completes when the current run, if any, has finished and cleaned up.</summary>
    public Task WaitForCompletionAsync()
    {
        lock (gate)
        {
            return run?.Completion ?? Task.CompletedTask;
        }
    }

    public SmokeRunInfo GetSnapshot()
    {
        lock (gate)
        {
            return new SmokeRunInfo
            {
                RunId = run?.RunId,
                Running = run is { FinishedAt: null },
                StartedAt = run?.StartedAt.ToString("O"),
                FinishedAt = run?.FinishedAt?.ToString("O"),
                Checks = [.. states.Select(s => s.ToInfo())]
            };
        }
    }

    async Task ExecuteAsync(RunState current)
    {
        try
        {
            foreach (var state in states.Where(s => !s.Check.IsManual))
            {
                if (current.Cancellation.IsCancellationRequested)
                {
                    Update(state, SmokeCheckStatus.Canceled, "The run was stopped before this check started");
                    continue;
                }

                await RunCheckAsync(current, state);
            }
        }
        finally
        {
            lock (gate)
            {
                current.FinishedAt = DateTimeOffset.UtcNow;
            }

            current.Cancellation.Dispose();
        }
    }

    async Task RunCheckAsync(RunState current, CheckState state)
    {
        Update(state, SmokeCheckStatus.Running, null);
        var started = DateTimeOffset.UtcNow;
        var context = new SmokeContext(current.RunId, pollInterval, pollTimeout);
        var timeout = state.Check.Timeout ?? defaultCheckTimeout;
        string? cleanupProblem = null;

        SmokeCheckStatus status;
        string? detail;
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(current.Cancellation.Token);
            timeoutSource.CancelAfter(timeout);
            try
            {
                await state.Check.Run!(context, timeoutSource.Token);
                (status, detail) = (SmokeCheckStatus.Passed, context.Detail);
            }
            catch (OperationCanceledException) when (current.Cancellation.IsCancellationRequested)
            {
                (status, detail) = (SmokeCheckStatus.Canceled, "The run was stopped while this check was running");
            }
            catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
            {
                (status, detail) = (SmokeCheckStatus.Failed, $"The check did not finish within {timeout.TotalSeconds:0.#}s");
            }
        }
        catch (SmokeCheckFailedException ex)
        {
            (status, detail) = (SmokeCheckStatus.Failed, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Smoke check {Check} failed with an unexpected error", state.Check.Id);
            (status, detail) = (SmokeCheckStatus.Failed, $"{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            cleanupProblem = await RunCleanupsAsync(context);
        }

        if (cleanupProblem is not null)
        {
            detail = string.IsNullOrEmpty(detail) ? cleanupProblem : $"{detail} {cleanupProblem}";
        }

        Update(state, status, detail, DateTimeOffset.UtcNow - started);
    }

    async Task<string?> RunCleanupsAsync(SmokeContext context)
    {
        var failures = new List<string>();
        await RunAll(context.EndpointStops, failures);
        await RunAll(context.Cleanups, failures);
        return failures.Count == 0 ? null : $"Cleanup failed, leftover data may remain: {string.Join("; ", failures)}";
    }

    // Every action runs in reverse order of registration with the token of one shared time limit, and is awaited to the end.
    async Task RunAll(IReadOnlyList<Func<CancellationToken, Task>> actions, List<string> failures)
    {
        using var limit = new CancellationTokenSource(cleanupTimeout);
        foreach (var action in actions.Reverse())
        {
            try
            {
                await action(limit.Token);
            }
            catch (OperationCanceledException) when (limit.IsCancellationRequested)
            {
                failures.Add($"timed out after {cleanupTimeout.TotalSeconds:0.#}s");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Smoke cleanup failed");
                failures.Add(ex.Message);
            }
        }
    }

    void Update(CheckState state, SmokeCheckStatus status, string? detail, TimeSpan? duration = null)
    {
        lock (gate)
        {
            state.Status = status;
            state.Detail = detail;
            state.Duration = duration;
        }
    }

    sealed class RunState(string runId)
    {
        public string RunId { get; } = runId;
        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? FinishedAt { get; set; }
        public CancellationTokenSource Cancellation { get; } = new();
        public Task Completion { get; set; } = Task.CompletedTask;
    }

    sealed class CheckState(SmokeCheck check)
    {
        public SmokeCheck Check { get; } = check;
        public SmokeCheckStatus Status { get; set; } = check.IsManual ? SmokeCheckStatus.Manual : SmokeCheckStatus.Pending;
        public string? Detail { get; set; }
        public TimeSpan? Duration { get; set; }

        public void Reset() => (Status, Detail, Duration) = (Check.IsManual ? SmokeCheckStatus.Manual : SmokeCheckStatus.Pending, null, null);

        public SmokeCheckInfo ToInfo() => new()
        {
            Id = Check.Id,
            Title = Check.Title,
            Description = Check.Description,
            Status = Status,
            Detail = Detail,
            DurationSeconds = Duration is { } d ? Math.Round(d.TotalSeconds, 1) : null
        };
    }
}
