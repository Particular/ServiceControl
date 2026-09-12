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

    private CancellationTokenSource? _cts;
    private Task[]? _loops;
    private long _errorsWritten;
    private long _errorsFailed;
    private double _currentRate;
    private string? _activeScenario;
    private DateTimeOffset _startedAt;

    public bool IsRunning => _cts is not null;
    public long ErrorsWritten => Interlocked.Read(ref _errorsWritten);
    public long ErrorsFailed => Interlocked.Read(ref _errorsFailed);
    public double CurrentRate => _currentRate;
    public string? ActiveScenario => _activeScenario;

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

        if (IsRunning)
        {
            error = "Bypass writer is already running — stop it first";
            return false;
        }

        if (rate <= 0)
        {
            error = "Rate must be greater than 0";
            return false;
        }

        // Default to ProcessorCount workers. Each worker runs its own timer at rate/N so the
        // aggregate approaches the target. More workers = more concurrent sends = higher
        // throughput when individual sends have latency.
        var workerCount = parallelism is { } p and > 0 ? p : Environment.ProcessorCount;

        _activeScenario = scenarioName;
        _currentRate = rate;
        _startedAt = DateTimeOffset.UtcNow;

        var cts = duration is { } d
            ? new CancellationTokenSource(d)
            : new CancellationTokenSource();
        _cts = cts;

        _loops = new Task[workerCount];
        for (var i = 0; i < workerCount; i++)
        {
            var workerIndex = i;
            _loops[i] = Task.Run(() => WriteLoop(scenario, rate / workerCount, workerIndex, cts.Token));
        }

        _logger.LogInformation("Started bypass writer for scenario {Scenario} at {Rate:F1} msg/s{Duration} across {Workers} workers",
            scenarioName, rate, duration is null ? "" : $" for {duration.Value}", workerCount);

        error = null;
        return true;
    }

    /// <summary>Stops the bypass writer.</summary>
    public void Stop()
    {
        if (_cts is null) return;

        _cts.Cancel();

        // Await all worker loops before disposing the token so we don't dispose a CTS that's still
        // in flight inside _session.Send. Swallow the expected cancellation/timeout.
        var loops = _loops;
        if (loops is not null)
        {
            try
            {
                Task.WaitAll(loops, TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Loops were cancelled or timed out — expected during stop.
            }
        }

        _cts.Dispose();
        _cts = null;
        _loops = null;

        _logger.LogInformation("Stopped bypass writer: {Errors} written, {Failed} failed", ErrorsWritten, ErrorsFailed);

        _activeScenario = null;
        _currentRate = 0;
    }

    /// <summary>Returns the current bypass writer status for API consumers.</summary>
    public BypassStatus GetStatus() => new()
    {
        Running = IsRunning,
        Scenario = _activeScenario,
        Rate = _currentRate,
        ErrorsWritten = ErrorsWritten,
        ErrorsFailed = ErrorsFailed,
        StartedAt = IsRunning ? _startedAt.ToString("O") : null
    };

    /// <summary>
    /// The load generation loop: sends <see cref="LoadMessage"/> directly to the error queue
    /// with failure headers at the target rate until cancelled. Multiple instances run in
    /// parallel, each handling a fraction of the total rate.
    /// </summary>
    private async Task WriteLoop(IScenario scenario, double rate, int workerIndex, CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(1.0 / rate);
        using var timer = new PeriodicTimer(interval);
        long sequence = 0;

        // Pre-compute the failure metadata from the scenario so all messages in this run
        // share the same exception type, message, and correlation group — ServiceControl
        // will group them as one error group.
        var exception = scenario.CreateException();
        var scenarioEx = exception as ScenarioException;
        var exceptionType = scenarioEx?.ExceptionType ?? exception.GetType().FullName!;
        var exceptionMessage = exception.Message;
        var correlationGroup = scenarioEx?.CorrelationGroup ?? "";

        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                var seq = Interlocked.Increment(ref sequence);

                // Plausible JSON body (~3–6 KB) seeded with searchable terms so the bypass path
                // also feeds ServiceControl's full-text search index with real content.
                var textBody = MessageTextGenerator.GenerateBody(seq);

                var message = new LoadMessage { Sequence = seq, TextBody = textBody };

                var sendOptions = new SendOptions();
                // Route directly to the ServiceControl error queue — bypasses the handler entirely.
                sendOptions.SetDestination(_options.ErrorQueueName);

                // Set failure headers so ServiceControl recognises the message as a failed message
                // and groups it by exception type + correlation group, exactly like handler failures.
                sendOptions.SetHeader("TestingTool.Scenario", scenario.Name);
                sendOptions.SetHeader("TestingTool.Bypass", "true");
                sendOptions.SetHeader("TestingTool.CorrelationGroup", correlationGroup);
                sendOptions.SetHeader("NServiceBus.ExceptionInfo.ExceptionType", exceptionType);
                sendOptions.SetHeader("NServiceBus.ExceptionInfo.Message", exceptionMessage);
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
        catch (OperationCanceledException) { }
    }
}