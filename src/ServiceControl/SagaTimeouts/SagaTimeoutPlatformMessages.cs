#nullable enable
// Endpoints report saga timeout changes with these contracts. The namespace and type names must match the
// endpoint's copies, because NServiceBus.EnclosedMessageTypes carries only the full type name.
namespace NServiceBus;

using System;
using System.Text.Json.Serialization;

abstract class SagaTimeoutPlatformMessage : IMessage
{
    public required string EndpointName { get; set; }
    public required string SagaType { get; set; }
    public required Guid SagaId { get; set; }
    public string? CorrelationPropertyValue { get; set; }
    public required DateTimeOffset ChangedAt { get; set; }
    public required string Host { get; set; }
    public required string HostId { get; set; }
    public required string ManagementAddress { get; set; }
}

abstract class SagaTimeoutChangeMessage : SagaTimeoutPlatformMessage
{
    public required string TimeoutId { get; set; }
    public required string TimeoutType { get; set; }
    public required int Version { get; set; }
}

sealed class SagaTimeoutRequested : SagaTimeoutChangeMessage
{
    public required string TriggerId { get; set; }
    public required DateTimeOffset DueTime { get; set; }
    public required string PayloadContentType { get; set; }
    public required byte[] Payload { get; set; }
}

sealed class SagaTimeoutRescheduled : SagaTimeoutChangeMessage
{
    public Guid? OperationId { get; set; }
    public required string TriggerId { get; set; }
    public required DateTimeOffset DueTime { get; set; }
    public required string PayloadContentType { get; set; }
    public required byte[] Payload { get; set; }
}

sealed class SagaTimeoutCanceled : SagaTimeoutChangeMessage
{
    public Guid? OperationId { get; set; }
}

sealed class SagaTimeoutTriggered : SagaTimeoutChangeMessage
{
    public required string TriggerId { get; set; }
}

sealed class SagaTimeoutChangeRejected : SagaTimeoutPlatformMessage
{
    public Guid? OperationId { get; set; }
    public required string TimeoutId { get; set; }
    public required string RequestedChange { get; set; }
    public DateTimeOffset? RequestedDueTime { get; set; }
    public required string Reason { get; set; }
}

sealed class SagaCompleted : SagaTimeoutPlatformMessage;

[JsonSerializable(typeof(SagaTimeoutRequested))]
[JsonSerializable(typeof(SagaTimeoutRescheduled))]
[JsonSerializable(typeof(SagaTimeoutCanceled))]
[JsonSerializable(typeof(SagaTimeoutTriggered))]
[JsonSerializable(typeof(SagaTimeoutChangeRejected))]
[JsonSerializable(typeof(SagaCompleted))]
partial class SagaTimeoutMessagesSerializationContext : JsonSerializerContext;
