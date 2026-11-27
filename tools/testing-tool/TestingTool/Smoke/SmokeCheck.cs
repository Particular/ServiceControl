namespace TestingTool.Smoke;

/// <summary>
/// One row of the smoke checklist. A null <see cref="Run"/> marks a manual row that the runner
/// lists but never executes.
/// </summary>
public sealed record SmokeCheck(
    string Id,
    string Title,
    string Description,
    Func<SmokeContext, CancellationToken, Task>? Run,
    TimeSpan? Timeout = null)
{
    public bool IsManual => Run is null;

    public static SmokeCheck Manual(string id, string title, string description) => new(id, title, description, null);
}

/// <summary>A check's expectation was not met. The message is shown in the UI as the failure detail.</summary>
public sealed class SmokeCheckFailedException(string message) : Exception(message);

public readonly record struct PollResult<T>(bool IsDone, T? Value, string Observed);

public static class PollResult
{
    public static PollResult<T> Done<T>(T value) => new(true, value, "");

    public static PollResult<T> Waiting<T>(string observed) => new(false, default, observed);
}

/// <summary>
/// Per-check state: the run identifier that scopes everything the check creates, bounded polling,
/// and cleanup actions the runner always executes after the check.
/// </summary>
public sealed class SmokeContext(string runId, TimeSpan pollInterval, TimeSpan pollTimeout)
{
    readonly List<Func<CancellationToken, Task>> cleanups = [];
    readonly List<Func<CancellationToken, Task>> endpointStops = [];

    public string RunId { get; } = runId;

    /// <summary>Text shown as the detail of a passed check.</summary>
    public string? Detail { get; set; }

    /// <summary>Registers an action the runner always awaits after the check. Its token cancels when the cleanup time limit passes.</summary>
    public void OnCleanup(Func<CancellationToken, Task> cleanup) => cleanups.Add(cleanup);

    /// <summary>
    /// Registers the stop of an endpoint host the check started. The runner awaits all stops before any
    /// cleanup, so an unavailable API cannot use up the time that stopping the hosts needs.
    /// </summary>
    public void OnEndpointStop(Func<CancellationToken, Task> stop) => endpointStops.Add(stop);

    public IReadOnlyList<Func<CancellationToken, Task>> EndpointStops => endpointStops;

    public IReadOnlyList<Func<CancellationToken, Task>> Cleanups => cleanups;

    /// <summary>
    /// Calls <paramref name="probe"/> until it reports done or <see cref="pollTimeout"/> passes.
    /// Exceptions from the probe, such as an API or transport error, fail the check immediately.
    /// </summary>
    public async Task<T> WaitFor<T>(string description, Func<CancellationToken, Task<PollResult<T>>> probe, CancellationToken ct, TimeSpan? timeout = null)
    {
        var limit = timeout ?? pollTimeout;
        var started = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            var result = await probe(ct);
            if (result.IsDone)
            {
                return result.Value!;
            }

            if (started.Elapsed >= limit)
            {
                throw new SmokeCheckFailedException($"Timed out after {limit.TotalSeconds:0.#}s waiting for {description}. Last observed: {result.Observed}");
            }

            await Task.Delay(pollInterval, ct);
        }
    }
}
