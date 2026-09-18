namespace ServiceControl.Persistence.EFCore.Implementation.Audit;

using System.Text.Json;
using System.Text.Json.Serialization;
using ServiceControl.SagaAudit;

public static class SagaSnapshotJson
{
    public static string? Write(InitiatingMessage? initiatingMessage) =>
        initiatingMessage is null ? null : JsonSerializer.Serialize(initiatingMessage, SagaSnapshotJsonContext.Default.InitiatingMessage);

    public static string Write(List<ResultingMessage> outgoingMessages) =>
        JsonSerializer.Serialize(outgoingMessages, SagaSnapshotJsonContext.Default.ListResultingMessage);

    public static InitiatingMessage? ReadInitiatingMessage(string? json) =>
        json is null ? null : JsonSerializer.Deserialize(json, SagaSnapshotJsonContext.Default.InitiatingMessage);

    public static List<ResultingMessage> ReadOutgoingMessages(string? json) =>
        json is null ? [] : JsonSerializer.Deserialize(json, SagaSnapshotJsonContext.Default.ListResultingMessage) ?? [];
}

// Source generated serialization, which keeps the reflection-based serializer off the ingestion hot path.
[JsonSerializable(typeof(InitiatingMessage))]
[JsonSerializable(typeof(List<ResultingMessage>))]
partial class SagaSnapshotJsonContext : JsonSerializerContext;
