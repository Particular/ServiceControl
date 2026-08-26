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
    private Task? _loop;
    private long _errorsWritten;
    private double _currentRate;
    private string? _activeScenario;
    private DateTimeOffset _startedAt;

    public bool IsRunning => _cts is not null;
    public long ErrorsWritten => Interlocked.Read(ref _errorsWritten);
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
    public bool TryStart(string scenarioName, double rate, TimeSpan? duration, out string? error)
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

        _activeScenario = scenarioName;
        _currentRate = rate;
        _startedAt = DateTimeOffset.UtcNow;

        var cts = duration is { } d
            ? new CancellationTokenSource(d)
            : new CancellationTokenSource();
        _cts = cts;

        _loop = Task.Run(() => WriteLoop(scenario, rate, cts.Token));

        _logger.LogInformation("Started bypass writer for scenario {Scenario} at {Rate:F1} msg/s{Duration}",
            scenarioName, rate, duration is null ? "" : $" for {duration.Value}");

        error = null;
        return true;
    }

    /// <summary>Stops the bypass writer.</summary>
    public void Stop()
    {
        if (_cts is null) return;

        _cts.Cancel();
        _cts.Dispose();
        _cts = null;

        _logger.LogInformation("Stopped bypass writer after {Errors} errors written", ErrorsWritten);

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
        StartedAt = IsRunning ? _startedAt.ToString("O") : null
    };

    /// <summary>
    /// The load generation loop: sends <see cref="LoadMessage"/> directly to the error queue
    /// with failure headers at the target rate until cancelled.
    /// </summary>
    private async Task WriteLoop(IScenario scenario, double rate, CancellationToken ct)
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
                var payload = new byte[Random.Shared.Next(64, 512)];
                Random.Shared.NextBytes(payload);

                var message = new LoadMessage { Sequence = seq, Payload = payload };

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
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogDebug(ex, "Bypass send failed for scenario {Scenario} seq {Seq}", scenario.Name, seq);
                }
            }
        }
        catch (OperationCanceledException) { }
    }
}