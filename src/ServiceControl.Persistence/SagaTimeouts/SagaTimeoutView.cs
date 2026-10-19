#nullable enable
namespace ServiceControl.Persistence.SagaTimeouts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

// The API writes these enum values as their names ("Pending"), unlike the camel-case default of other APIs,
// because ServicePulse shares this contract.
public class SagaTimeoutView
{
    public required string TimeoutId { get; init; }
    public required string EndpointName { get; init; }
    public required string SagaType { get; init; }
    public Guid SagaId { get; init; }
    public string? CorrelationValue { get; init; }
    public string? TimeoutType { get; init; }
    public int Version { get; init; }
    public string? TriggerId { get; init; }
    public DateTime? DueTime { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SagaTimeoutStatus Status { get; init; }

    public bool IsOverdue { get; init; }
    public DateTime? RequestedAt { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SagaTimeoutChangeKind LastChangeKind { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SagaTimeoutChangeSource LastChangeSource { get; init; }

    public DateTime? LastChangedAt { get; init; }
    public string? Host { get; init; }
    public string? ManagementAddress { get; init; }

    // Written as null rather than left out, so clients can rely on the field
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public SagaTimeoutPendingOperationView? PendingOperation { get; init; }
}

public class SagaTimeoutPendingOperationView
{
    public Guid OperationId { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SagaTimeoutOperation Change { get; init; }

    public DateTime RequestedAt { get; init; }
    public DateTime? RequestedDueTime { get; init; }
}

public class SagaTimeoutDetails : SagaTimeoutView
{
    public string? PayloadContentType { get; init; }

    /// <summary>The current payload decoded as UTF-8.</summary>
    public string? Payload { get; init; }

    public bool HistoryTruncated { get; init; }

    public IList<SagaTimeoutHistoryEntryView> History { get; init; } = [];
}

public class SagaTimeoutHistoryEntryView
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SagaTimeoutChangeKind Kind { get; init; }

    public int Version { get; init; }
    public string? TriggerId { get; init; }
    public DateTime? DueTime { get; init; }
    public DateTime ChangedAt { get; init; }
    public DateTime ReceivedAt { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SagaTimeoutChangeSource Source { get; init; }

    public Guid? OperationId { get; init; }
    public string? RequestedChange { get; init; }
    public DateTime? RequestedDueTime { get; init; }
    public string? Reason { get; init; }
}

public class SagaTimeoutCounts
{
    public long Pending { get; init; }
    public long Overdue { get; init; }
}
