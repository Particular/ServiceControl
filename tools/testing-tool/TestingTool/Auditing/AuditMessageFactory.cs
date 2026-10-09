using System.Buffers;
using System.Text;
using System.Text.Json;
using NServiceBus;
using TestingTool.Scenarios;

namespace TestingTool.Auditing;

/// <summary>
/// Builds the wire messages the direct audit-queue writer puts on the ServiceControl.Audit
/// instance's audit queue: processed-message envelopes, and the saga episodes that link a run of
/// those envelopes to a saga's snapshot history.
/// </summary>
/// <remarks>
/// Endpoints and hosts are derived from the shard id alone, so they are stable across runs and
/// KnownEndpoints settles. Message, saga and conversation ids additionally take a per-run salt,
/// because the claim sequence restarts at 1 on every run: without it a restart would replay the
/// same ids and one saga would accumulate repeated New/Updated/Completed cycles.
/// </remarks>
public sealed class AuditMessageFactory
{
    /// <summary>
    /// The discriminator ServiceControl matches to route an audit message to its saga branch.
    /// AuditPersister compares <c>NServiceBus.EnclosedMessageTypes</c> to this with <c>==</c>, so an
    /// assembly-qualified or multi-type value is silently stored as an ordinary audit row instead,
    /// with no error and no saga history.
    /// </summary>
    public const string SagaUpdatedMessageType = "ServiceControl.EndpointPlugin.Messages.SagaState.SagaUpdatedMessage";

    /// <summary>Snapshots per saga: New, then Updated, then Completed.</summary>
    public const int PhasesPerSaga = 3;

    const string JsonContentType = "application/json";

    static readonly string[] EndpointNames =
    [
        "Sales", "Billing", "Shipping", "Warehouse", "Notifications", "Integration"
    ];

    static readonly string[] MessageTypes =
    [
        "Acme.Sales.Messages.PlaceOrder",
        "Acme.Sales.Messages.OrderPlaced",
        "Acme.Billing.Messages.ChargeCustomer",
        "Acme.Billing.Messages.PaymentSettled",
        "Acme.Shipping.Messages.DispatchConsignment",
        "Acme.Warehouse.Messages.ReserveInventory",
        "Acme.Notifications.Messages.SendConfirmation",
        "Acme.Integration.Messages.SyncLedgerEntry"
    ];

    static readonly string[] SagaTypes =
    [
        "Acme.Sales.Sagas.OrderFulfilmentSaga",
        "Acme.Billing.Sagas.SubscriptionRenewalSaga",
        "Acme.Shipping.Sagas.ConsignmentTrackingSaga"
    ];

    static readonly string[] PhaseStatus = ["New", "Updated", "Completed"];

    readonly SimulatedEndpoint[] endpoints;
    readonly string shardId;

    public AuditMessageFactory(string shardId)
    {
        this.shardId = shardId;
        endpoints = [.. EndpointNames.Select(name =>
            SimulatedEndpoint.Create($"{name}.Load-{shardId}", $"host-{shardId}-{name.ToLowerInvariant()}"))];
    }

    /// <summary>
    /// Builds an envelope for a message an endpoint processed successfully, with no saga involvement.
    /// </summary>
    public RawAuditPayload CreateProcessedMessage(long runSalt, long sequence) =>
        BuildProcessedMessage(runSalt, sequence, saga: null);

    /// <summary>
    /// Builds one saga's whole life: for each of New, Updated and Completed, the message that drove
    /// the change followed by the snapshot it produced.
    /// </summary>
    /// <remarks>
    /// The episode is built as a unit so a single worker writes the phases in order. Handing phases
    /// out from a shared counter lets Completed overtake New once more than one worker is running,
    /// and the saga history then reads backwards.
    /// </remarks>
    public IReadOnlyList<RawAuditPayload> CreateSagaEpisode(long runSalt, long episodeOrdinal, long sequence)
    {
        // Selecting on the claim number would collapse these pools: only sequences that are a
        // multiple of the episode period ever get here, so their residues are not uniform and every
        // episode of a run can land on the same endpoint and saga type.
        var ordinal = episodeOrdinal;
        var endpoint = endpoints[(int)(ordinal % endpoints.Length)];
        var sagaType = SagaTypes[(int)(ordinal % SagaTypes.Length)];
        var sagaId = SimulatedEndpoint.DeterministicGuid($"{shardId}/{runSalt}/{endpoint.Name}/{sagaType}/{ordinal}");

        // A saga that starts now and advances in milliseconds, not one backdated by minutes: the
        // saga history view orders by FinishTime, and the audit tables are partitioned by ingestion
        // hour, so timestamps have to stay near wall clock.
        var start = DateTimeOffset.UtcNow;

        var episode = new List<RawAuditPayload>(PhasesPerSaga * 2);
        for (var phase = 0; phase < PhasesPerSaga; phase++)
        {
            var saga = new SagaParticipation(sagaId, sagaType, endpoint, PhaseStatus[phase], start, phase);
            var message = BuildProcessedMessage(runSalt, sequence, saga);
            episode.Add(message);
            episode.Add(BuildSagaSnapshot(runSalt, saga, message.Headers[Headers.MessageId], sequence + phase));
        }

        return episode;
    }

    /// <summary>
    /// The number of claims between saga episodes that yields the requested snapshot-to-message
    /// ratio. A period of p produces p - 1 plain messages plus one episode, and an episode is
    /// <see cref="PhasesPerSaga"/> snapshots alongside the same number of processed messages, so
    /// the ratio is <c>P / (p - 1 + P)</c> and the period that solves it is <c>P/r - P + 1</c>.
    /// </summary>
    public static int SagaEpisodePeriod(double sagaSnapshotRatio) =>
        sagaSnapshotRatio <= 0 ? 0 : Math.Max(1, (int)Math.Round(PhasesPerSaga / sagaSnapshotRatio - PhasesPerSaga + 1, MidpointRounding.AwayFromZero));

    RawAuditPayload BuildProcessedMessage(long runSalt, long sequence, SagaParticipation? saga)
    {
        var receiver = saga?.Endpoint ?? endpoints[(int)(sequence % endpoints.Length)];
        var sender = endpoints[(int)(sequence / endpoints.Length % endpoints.Length)];
        var messageType = MessageTypes[(int)(sequence % MessageTypes.Length)];

        var processingEnded = saga?.PhaseTime ?? DateTimeOffset.UtcNow;
        var processingStarted = processingEnded.AddMilliseconds(-(5 + sequence % 120));
        var timeSent = processingStarted.AddMilliseconds(-(10 + sequence % 400));

        var messageId = SimulatedEndpoint.DeterministicGuid(
            $"{shardId}/{runSalt}/msg/{sequence}/{saga?.SagaId.ToString() ?? "none"}/{saga?.Phase ?? 0}").ToString();

        var headers = CommonHeaders(runSalt, sequence, messageId, receiver, sender, timeSent, processingStarted, processingEnded);
        headers[Headers.EnclosedMessageTypes] = messageType;
        headers["TestingTool.MessageType"] = messageType;

        if (saga is not null)
        {
            // These two headers are the only thing that links an audited message to a saga: the EF
            // persister rebuilds InvokedSagas from them at query time. The shapes are strict, and a
            // malformed value throws inside the per-row projection, which 500s a whole page of
            // /api/messages after the rows are already stored.
            headers["NServiceBus.InvokedSagas"] = $"{saga.SagaType}:{saga.SagaId}";
            headers["ServiceControl.SagaStateChange"] = $"{saga.SagaId}:{saga.Status}";
        }

        var body = Encoding.UTF8.GetBytes(MessageTextGenerator.GenerateBody(sequence));
        return new RawAuditPayload(headers, body);
    }

    RawAuditPayload BuildSagaSnapshot(long runSalt, SagaParticipation saga, string initiatingMessageId, long sequence)
    {
        var headers = CommonHeaders(
            runSalt,
            sequence,
            SimulatedEndpoint.DeterministicGuid($"{shardId}/{runSalt}/snapshot/{saga.SagaId}/{saga.Phase}").ToString(),
            saga.Endpoint,
            saga.Endpoint,
            saga.PhaseTime.AddMilliseconds(-20),
            saga.PhaseTime.AddMilliseconds(-10),
            saga.PhaseTime);

        headers[Headers.EnclosedMessageTypes] = SagaUpdatedMessageType;
        headers["TestingTool.MessageType"] = SagaUpdatedMessageType;

        var body = WriteSagaUpdatedMessage(saga, initiatingMessageId, sequence);
        return new RawAuditPayload(headers, body);
    }

    Dictionary<string, string> CommonHeaders(
        long runSalt,
        long sequence,
        string messageId,
        SimulatedEndpoint receiver,
        SimulatedEndpoint sender,
        DateTimeOffset timeSent,
        DateTimeOffset processingStarted,
        DateTimeOffset processingEnded) => new()
        {
            [Headers.MessageId] = messageId,
            [Headers.ProcessingEndpoint] = receiver.Name,
            [Headers.ProcessingMachine] = receiver.Machine,
            [Headers.HostId] = receiver.HostId.ToString("N"),
            [Headers.HostDisplayName] = receiver.HostDisplayName,
            [Headers.OriginatingEndpoint] = sender.Name,
            [Headers.OriginatingMachine] = sender.Machine,
            [Headers.OriginatingHostId] = sender.HostId.ToString("N"),
            [Headers.TimeSent] = DateTimeOffsetHelper.ToWireFormattedString(timeSent),
            [Headers.ProcessingStarted] = DateTimeOffsetHelper.ToWireFormattedString(processingStarted),
            [Headers.ProcessingEnded] = DateTimeOffsetHelper.ToWireFormattedString(processingEnded),
            [Headers.ContentType] = JsonContentType,
            [Headers.MessageIntent] = nameof(MessageIntent.Send),
            // Five consecutive messages share a conversation so the message-flow view has a graph
            // to draw rather than a run of singletons.
            [Headers.ConversationId] = SimulatedEndpoint.DeterministicGuid($"{shardId}/{runSalt}/conversation/{sequence / 5}").ToString(),
            ["TestingTool.Bypass"] = "true"
        };

    static byte[] WriteSagaUpdatedMessage(SagaParticipation saga, string initiatingMessageId, long sequence)
    {
        // Property names are written in the CLR casing on purpose: ServiceControl deserializes with
        // a source-generated context that uses the default, case-sensitive naming policy. A
        // mis-cased scalar silently becomes a default; a mis-cased Initiator or a null
        // ResultingMessages throws inside SagaSnapshotFactory and lands the message in failed imports.
        var buffer = new ArrayBufferWriter<byte>(1024);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("SagaId", saga.SagaId);
            writer.WriteString("SagaType", saga.SagaType);
            writer.WriteString("Endpoint", saga.Endpoint.Name);
            writer.WriteBoolean("IsNew", saga.Phase == 0);
            writer.WriteBoolean("IsCompleted", saga.Phase == PhasesPerSaga - 1);
            writer.WriteString("StartTime", saga.StartTime.UtcDateTime);
            writer.WriteString("FinishTime", saga.PhaseTime.UtcDateTime);
            writer.WriteString("SagaState", SagaState(saga, sequence));

            writer.WriteStartObject("Initiator");
            writer.WriteString("InitiatingMessageId", initiatingMessageId);
            writer.WriteString("MessageType", MessageTypes[(int)(sequence % MessageTypes.Length)]);
            writer.WriteBoolean("IsSagaTimeoutMessage", false);
            writer.WriteString("TimeSent", saga.PhaseTime.UtcDateTime);
            writer.WriteString("OriginatingMachine", saga.Endpoint.Machine);
            writer.WriteString("OriginatingEndpoint", saga.Endpoint.Name);
            writer.WriteString("Intent", nameof(MessageIntent.Send));
            writer.WriteEndObject();

            writer.WriteStartArray("ResultingMessages");
            if (saga.Phase < PhasesPerSaga - 1)
            {
                writer.WriteStartObject();
                writer.WriteString("MessageType", MessageTypes[(int)((sequence + 1) % MessageTypes.Length)]);
                writer.WriteString("TimeSent", saga.PhaseTime.UtcDateTime);
                writer.WriteString("Destination", saga.Endpoint.Name);
                writer.WriteString("ResultingMessageId", initiatingMessageId);
                writer.WriteString("Intent", nameof(MessageIntent.Send));
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    static string SagaState(SagaParticipation saga, long sequence) =>
        $$"""{"Id":"{{saga.SagaId}}","Status":"{{saga.Status}}","OrderNumber":"ORD-{{sequence:D8}}","Originator":"load"}""";

    sealed record SagaParticipation(
        Guid SagaId,
        string SagaType,
        SimulatedEndpoint Endpoint,
        string Status,
        DateTimeOffset StartTime,
        int Phase)
    {
        /// <summary>When this phase finished. Phases are milliseconds apart, as a real saga's are.</summary>
        public DateTimeOffset PhaseTime { get; } = StartTime.AddMilliseconds(40 * Phase);
    }
}
