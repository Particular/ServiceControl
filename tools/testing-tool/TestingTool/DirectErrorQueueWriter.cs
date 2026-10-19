using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NServiceBus;
using TestingTool.Contracts;
using TestingTool.Scenarios;

namespace TestingTool;

/// <summary>
/// Bypass path: constructs failed-message envelopes with NServiceBus failure headers and writes
/// them directly to the ServiceControl error queue via the NServiceBus transport, without going
/// through the handler. This enables high-throughput error load generation that bypasses the
/// initial message creation and handler processing (requirement: "simulate high error loads,
/// bypass actually creating the initial messages").
///
/// Each emitted message carries the standard NServiceBus failure headers
/// (<c>NServiceBus.ExceptionInfo.*</c>, <c>NServiceBus.FailedQ</c>) so ServiceControl ingests
/// it as a genuine failed message and groups it by the scenario's exception type and correlation
/// group — exactly like handler-generated failures.
/// </summary>
public sealed class DirectErrorQueueWriter
{
    private readonly IMessageSession _session;
    private readonly IScenarioRegistry _registry;
    private readonly TestingToolMetrics _metrics;
    private readonly Meter _meter;
    private readonly TestingToolOptions _options;
    private readonly ILogger<DirectErrorQueueWriter> _logger;

    private readonly ActivitySource _activitySource = new(TelemetrySetup.Sources.Bypass);
    private readonly Counter<long> _bypassCounter;

    // The current run, or null when idle. Swapped atomically so Stop() and a run ending on its
    // own (duration elapsed or crash) can't both tear it down.
    private BypassRun? _run;
    private long _errorsWritten;
    private long _errorsFailed;

    public bool IsRunning => Volatile.Read(ref _run) is not null;
    public long ErrorsWritten => Interlocked.Read(ref _errorsWritten);
    public long ErrorsFailed => Interlocked.Read(ref _errorsFailed);
    public double CurrentRate => Volatile.Read(ref _run)?.Rate ?? 0;
    public string? ActiveScenario => Volatile.Read(ref _run)?.Scenario.Name;

    public DirectErrorQueueWriter(
        IMessageSession session,
        IScenarioRegistry registry,
        TestingToolMetrics metrics,
        Meter meter,
        IOptions<TestingToolOptions> options,
        ILogger<DirectErrorQueueWriter> logger)
    {
        _session = session;
        _registry = registry;
        _metrics = metrics;
        _meter = meter;
        _options = options.Value;
        _logger = logger;
        _bypassCounter = meter.CreateCounter<long>("bypass_errors_written_total");
    }

    /// <summary>Starts writing failed-message envelopes directly to the error queue.</summary>
    public bool TryStart(string scenarioName, double rate, TimeSpan? duration, int? parallelism, out string? error)
    {
        var scenario = _registry.Get(scenarioName);
        if (scenario is null)
        {
            error = $"Unknown scenario '{scenarioName}'";
            return false;
        }

        if (rate <= 0)
        {
            error = "Rate must be greater than 0";
            return false;
        }

        // Default to ProcessorCount workers. Workers draw from a shared send budget, so more
        // workers = more concurrent sends = higher throughput when individual sends have latency.
        var workerCount = parallelism is { } p and > 0 ? p : Environment.ProcessorCount;

        var run = new BypassRun(scenario, rate, workerCount, duration);
        if (Interlocked.CompareExchange(ref _run, run, null) is not null)
        {
            run.Cts.Dispose();
            error = "Bypass writer is already running — stop it first";
            return false;
        }

        var token = run.Cts.Token;
        run.Loops = new Task[workerCount];
        for (var i = 0; i < workerCount; i++)
        {
            var workerIndex = i;
            run.Loops[i] = Task.Run(() => WriteLoop(run, workerIndex, token));
        }
        Task.WhenAll(run.Loops).ContinueWith(t => OnRunCompleted(run, t), TaskScheduler.Default);

        _logger.LogInformation("Started bypass writer for scenario {Scenario} at {Rate:F1} msg/s{Duration} across {Workers} workers",
            scenarioName, rate, duration is null ? "" : $" for {duration.Value}", workerCount);

        error = null;
        return true;
    }

    /// <summary>Stops the bypass writer.</summary>
    public void Stop()
    {
        var run = Interlocked.Exchange(ref _run, null);
        if (run is null) return;

        run.Cts.Cancel();

        // Await all worker loops before disposing the token so we don't dispose a CTS that's still
        // in flight inside _session.Send. Swallow the expected cancellation/timeout.
        // Loops is null only if Stop() races TryStart between publishing the run and starting
        // its workers; those workers then see the cancelled token and exit immediately.
        try
        {
            if (run.Loops is { } loops)
                Task.WaitAll(loops, TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Loops were cancelled or timed out — expected during stop.
        }

        run.Cts.Dispose();

        _logger.LogInformation("Stopped bypass writer: {Written} written this run, {Errors} written, {Failed} failed in total",
            run.Written, ErrorsWritten, ErrorsFailed);
    }

    /// <summary>
    /// Runs when every worker has exited. Logs crashes (worker tasks are otherwise unobserved)
    /// and, if the run ended on its own rather than via <see cref="Stop"/>, clears it so status
    /// stops reporting it as running.
    /// </summary>
    private void OnRunCompleted(BypassRun run, Task workers)
    {
        if (workers.Exception is { } ex)
        {
            _logger.LogError(ex.Flatten(), "Bypass writer for scenario {Scenario} crashed", run.Scenario.Name);
        }

        if (Interlocked.CompareExchange(ref _run, null, run) == run)
        {
            run.Cts.Dispose();
            _logger.LogInformation("Bypass writer for scenario {Scenario} finished: {Written} written this run, {Failed} failed in total",
                run.Scenario.Name, run.Written, ErrorsFailed);
        }
    }

    /// <summary>Returns the current bypass writer status for API consumers.</summary>
    public BypassStatus GetStatus()
    {
        var run = Volatile.Read(ref _run);
        return new()
        {
            Running = run is not null,
            Scenario = run?.Scenario.Name,
            Rate = run?.Rate ?? 0,
            AchievedRate = Math.Round(run?.AchievedRate ?? 0, 1),
            ErrorsWritten = ErrorsWritten,
            ErrorsFailed = ErrorsFailed,
            StartedAt = run?.StartedAt.ToString("O")
        };
    }

    /// <summary>
    /// The load generation loop: sends <see cref="LoadMessage"/> directly to the error queue
    /// with failure headers at the target rate until cancelled. Multiple instances run in
    /// parallel, drawing from the run's shared budget.
    /// Pacing is by elapsed time rather than one message per tick: on each tick a worker claims
    /// and sends whatever is owed (<c>floor(elapsed * rate) - claimed</c>), so timer coarseness
    /// (~15.6 ms on Windows) and send latency are caught up instead of lowering the rate.
    /// </summary>
    private async Task WriteLoop(BypassRun run, int workerIndex, CancellationToken ct)
    {
        var scenario = run.Scenario;
        using var timer = new PeriodicTimer(run.WorkerTick);

        // Pre-compute the failure metadata from the scenario so all messages in this run
        // share the same exception type and stack trace — ServiceControl groups on type + first
        // frame, so they land in the same group as the equivalent handler-path failures.
        var exception = scenario.CreateException();
        var exceptionType = exception.GetType().FullName!;
        var exceptionMessage = exception.Message;
        var stackTrace = exception.ToString();
        var correlationGroup = ScenarioBase.GetCorrelationGroup(exception) ?? "";

        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                while (!ct.IsCancellationRequested && run.TryClaim(out var seq))
                {
                    // Plausible JSON body (~3–6 KB) seeded with searchable terms so the bypass path
                    // also feeds ServiceControl's full-text search index with real content.
                    var textBody = MessageTextGenerator.GenerateBody(seq);

                    var message = new LoadMessage { Sequence = seq, TextBody = textBody };

                    var sendOptions = new SendOptions();
                    // Route directly to the ServiceControl error queue — bypasses the handler entirely.
                    sendOptions.SetDestination(_options.ErrorQueueName);

                    // Set failure headers so ServiceControl recognises the message as a failed message
                    // and groups it by exception type + stack trace, exactly like handler failures.
                    sendOptions.SetHeader("TestingTool.Scenario", scenario.Name);
                    sendOptions.SetHeader("TestingTool.Bypass", "true");
                    sendOptions.SetHeader("TestingTool.CorrelationGroup", correlationGroup);
                    sendOptions.SetHeader("NServiceBus.ExceptionInfo.ExceptionType", exceptionType);
                    sendOptions.SetHeader("NServiceBus.ExceptionInfo.Message", exceptionMessage);
                    sendOptions.SetHeader("NServiceBus.ExceptionInfo.StackTrace", stackTrace);
                    sendOptions.SetHeader("NServiceBus.ExceptionInfo.Source", "TestingTool.Load");
                    sendOptions.SetHeader("NServiceBus.FailedQ", "TestingTool.Load");

                    using var activity = _activitySource.StartActivity("bypass-write");
                    activity?.SetTag("scenario", scenario.Name);
                    activity?.SetTag("sequence", seq);
                    activity?.SetTag("exception.type", exceptionType);
                    activity?.SetTag("exception.group", correlationGroup);

                    try
                    {
                        await _session.Send(message, sendOptions, ct);

                        Interlocked.Increment(ref _errorsWritten);
                        run.IncrementWritten();
                        _metrics.AddErrorsSent(1);
                        _metrics.AddBypassErrorsWritten(1);
                        _bypassCounter.Add(1, new KeyValuePair<string, object?>("scenario", scenario.Name));
                    }
                    catch (OperationCanceledException)
                    {
                        // Cancellation is expected on stop/timeout — let it propagate to the outer handler.
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref _errorsFailed);
                        _metrics.AddBypassErrorsFailed(1);

                        // Log at Warning so send failures are visible in default logging configs.
                        // Previously this was LogDebug, which silently swallowed transport/broker
                        // failures and made the bypass appear idle when sends were actually failing.
                        _logger.LogWarning(ex, "Bypass send failed for scenario {Scenario} worker {Worker} seq {Seq}", scenario.Name, workerIndex, seq);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
    }
}
/// <summary>Adds the scenario a bypass run is generating failures for to the shared run state.</summary>
internal sealed class BypassRun(IScenario scenario, double rate, int workerCount, TimeSpan? duration)
    : LoadRun(rate, workerCount, duration)
{
    public IScenario Scenario { get; } = scenario;
}
