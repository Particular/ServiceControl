using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NServiceBus;
using NServiceBus.Extensibility;
using TestingTool.Auditing;
using TestingTool.Contracts;

namespace TestingTool;

/// <summary>
/// The audit-queue counterpart of <see cref="DirectErrorQueueWriter"/>: writes audit envelopes
/// straight to the ServiceControl.Audit instance's audit queue over the transport, so audit
/// ingestion can be loaded without any endpoint actually processing messages.
///
/// Most of what it emits are processed-message envelopes carrying the headers a real endpoint
/// would have stamped. A configurable share of claims instead emit a whole saga episode: three
/// messages that drove a saga through New, Updated and Completed, each followed by the snapshot it
/// produced, with the headers that link the messages to the saga.
/// </summary>
public sealed class DirectAuditQueueWriter
{
    private readonly IMessageSession _session;
    private readonly TestingToolMetrics _metrics;
    private readonly TestingToolOptions _options;
    private readonly AuditMessageFactory _factory;
    private readonly ILogger<DirectAuditQueueWriter> _logger;

    private readonly ActivitySource _activitySource = new(TelemetrySetup.Sources.AuditBypass);
    private readonly Counter<long> _auditCounter;

    private AuditRun? _run;
    private long _messagesWritten;
    private long _snapshotsWritten;
    private long _messagesFailed;

    public bool IsRunning => Volatile.Read(ref _run) is not null;
    public long MessagesWritten => Interlocked.Read(ref _messagesWritten);
    public long SnapshotsWritten => Interlocked.Read(ref _snapshotsWritten);
    public long MessagesFailed => Interlocked.Read(ref _messagesFailed);

    public DirectAuditQueueWriter(
        IMessageSession session,
        TestingToolMetrics metrics,
        Meter meter,
        IOptions<TestingToolOptions> options,
        AuditMessageFactory factory,
        ILogger<DirectAuditQueueWriter> logger)
    {
        _session = session;
        _metrics = metrics;
        _options = options.Value;
        _factory = factory;
        _logger = logger;
        _auditCounter = meter.CreateCounter<long>("bypass_audit_messages_written_total");
    }

    /// <summary>Starts writing audit envelopes directly to the audit queue.</summary>
    /// <param name="sagaSnapshotRatio">
    /// Saga snapshots per processed message. Null falls back to the configured default.
    /// </param>
    public bool TryStart(double rate, TimeSpan? duration, int? parallelism, double? sagaSnapshotRatio, out string? error)
    {
        if (rate <= 0)
        {
            error = "Rate must be greater than 0";
            return false;
        }

        var ratio = sagaSnapshotRatio ?? _options.AuditSagaSnapshotRatio;
        if (ratio is < 0 or > 1)
        {
            error = "Saga snapshot ratio must be between 0 and 1";
            return false;
        }

        var workerCount = parallelism is { } p and > 0 ? p : Environment.ProcessorCount;

        var run = new AuditRun(rate, workerCount, duration, ratio);

        // Read the token and build the worker array before publishing the run. Publishing first
        // leaves a window where Stop() takes the run, disposes the CTS and returns while this
        // method is still about to read Cts.Token, which then throws ObjectDisposedException out
        // of the API handler and leaves the writer idle with a bare 500.
        var token = run.Cts.Token;
        var loops = new Task[workerCount];
        run.Loops = loops;

        if (Interlocked.CompareExchange(ref _run, run, null) is not null)
        {
            run.Cts.Dispose();
            error = "Audit-queue writer is already running — stop it first";
            return false;
        }

        for (var i = 0; i < workerCount; i++)
        {
            var workerIndex = i;
            loops[i] = Task.Run(() => WriteLoop(run, workerIndex, token));
        }
        Task.WhenAll(loops).ContinueWith(t => OnRunCompleted(run, t), TaskScheduler.Default);

        _logger.LogInformation(
            "Started audit-queue writer at {Rate:F1} claims/s{Duration} across {Workers} workers, saga episode every {Period} claims",
            rate, duration is null ? "" : $" for {duration.Value}", workerCount,
            run.SagaEpisodePeriod == 0 ? "never" : run.SagaEpisodePeriod.ToString());

        error = null;
        return true;
    }

    /// <summary>Stops the audit-queue writer.</summary>
    public void Stop()
    {
        var run = Interlocked.Exchange(ref _run, null);
        if (run is null) return;

        run.Cts.Cancel();

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

        _logger.LogInformation("Stopped audit-queue writer: {Messages} messages, {Snapshots} saga snapshots, {Failed} failed in total",
            MessagesWritten, SnapshotsWritten, MessagesFailed);
    }

    private void OnRunCompleted(AuditRun run, Task workers)
    {
        if (workers.Exception is { } ex)
        {
            _logger.LogError(ex.Flatten(), "Audit-queue writer crashed");
        }

        if (Interlocked.CompareExchange(ref _run, null, run) == run)
        {
            run.Cts.Dispose();
            _logger.LogInformation("Audit-queue writer finished: {Written} written this run, {Failed} failed in total",
                run.Written, MessagesFailed);
        }
    }

    /// <summary>Returns the current audit writer status for API consumers.</summary>
    public AuditBypassStatus GetStatus()
    {
        var run = Volatile.Read(ref _run);
        return new()
        {
            Running = run is not null,
            Rate = run?.Rate ?? 0,
            AchievedRate = Math.Round(run?.AchievedRate ?? 0, 1),
            SagaSnapshotRatio = run?.SagaSnapshotRatio ?? 0,
            ProcessedMessagesWritten = MessagesWritten,
            SagaSnapshotsWritten = SnapshotsWritten,
            MessagesFailed = MessagesFailed,
            StartedAt = run?.StartedAt.ToString("O")
        };
    }

    private async Task WriteLoop(AuditRun run, int workerIndex, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(run.WorkerTick);

        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                while (!ct.IsCancellationRequested && run.TryClaim(out var seq))
                {
                    var isSagaEpisode = run.SagaEpisodePeriod > 0 && seq % run.SagaEpisodePeriod == 0;

                    // A saga episode is written whole by this one worker so its New, Updated and
                    // Completed phases reach the queue in order.
                    var payloads = isSagaEpisode
                        ? _factory.CreateSagaEpisode(run.Salt, seq / run.SagaEpisodePeriod, seq)
                        : [_factory.CreateProcessedMessage(run.Salt, seq)];

                    foreach (var payload in payloads)
                    {
                        if (ct.IsCancellationRequested) break;
                        await Write(run, payload, seq, workerIndex, ct);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private async Task Write(AuditRun run, RawAuditPayload payload, long sequence, int workerIndex, CancellationToken ct)
    {
        var isSnapshot = payload.Headers[Headers.EnclosedMessageTypes] == AuditMessageFactory.SagaUpdatedMessageType;
        var kind = isSnapshot ? SagaSnapshotKind : ProcessedMessageKind;

        var sendOptions = new SendOptions();
        sendOptions.SetDestination(_options.AuditQueueName);
        sendOptions.GetExtensions().Set(payload);

        using var activity = _activitySource.StartActivity("audit-write");
        activity?.SetTag("sequence", sequence);
        activity?.SetTag("audit.kind", kind);

        try
        {
            await _session.Send(new AuditEnvelope { Sequence = sequence }, sendOptions, ct);

            run.IncrementWritten();
            if (isSnapshot)
            {
                Interlocked.Increment(ref _snapshotsWritten);
                _metrics.AddAuditSagaSnapshotsWritten(1);
            }
            else
            {
                Interlocked.Increment(ref _messagesWritten);
                _metrics.AddAuditMessagesWritten(1);
            }
            _auditCounter.Add(1, new KeyValuePair<string, object?>("kind", kind));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Our own stop or duration expiry — let it unwind the worker.
            throw;
        }
        catch (Exception ex)
        {
            // A transport timeout surfaces as TaskCanceledException. Unfiltered, it would be
            // mistaken for a stop and silently retire the worker with nothing counted or logged.
            Interlocked.Increment(ref _messagesFailed);
            _metrics.AddAuditMessagesFailed(1);
            _logger.LogWarning(ex, "Audit-queue send failed on worker {Worker} seq {Seq} kind {Kind}", workerIndex, sequence, kind);
        }
    }

    private const string ProcessedMessageKind = "processed-message";
    private const string SagaSnapshotKind = "saga-snapshot";
}

/// <summary>Adds the saga mix to the shared run state.</summary>
internal sealed class AuditRun : LoadRun
{
    public AuditRun(double rate, int workerCount, TimeSpan? duration, double sagaSnapshotRatio)
        : base(rate, workerCount, duration)
    {
        SagaSnapshotRatio = sagaSnapshotRatio;
        SagaEpisodePeriod = AuditMessageFactory.SagaEpisodePeriod(sagaSnapshotRatio);
    }

    public double SagaSnapshotRatio { get; }

    /// <summary>Distinguishes one run's generated ids from the next, since claims restart at 1.</summary>
    public long Salt => StartedAt.Ticks;

    /// <summary>Claims between saga episodes, or 0 when snapshots are disabled.</summary>
    public int SagaEpisodePeriod { get; }
}
