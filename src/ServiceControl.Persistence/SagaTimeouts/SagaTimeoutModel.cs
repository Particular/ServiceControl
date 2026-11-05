#nullable enable
namespace ServiceControl.Persistence.SagaTimeouts;

using System;

public enum SagaTimeoutStatus
{
    Unknown,
    Pending,
    Triggered,
    Canceled
}

public enum SagaTimeoutChangeKind
{
    Requested,
    Rescheduled,
    Canceled,
    Triggered,
    Rejected
}

public enum SagaTimeoutChangeSource
{
    Saga,
    Operator
}

public enum SagaTimeoutOperation
{
    Reschedule,
    Cancel,
    Trigger
}

public enum SagaTimeoutApplyOutcome
{
    /// <summary>The change became the current state.</summary>
    Applied,
    /// <summary>The change was older than the current state, or is a rejection, so only its history entry was recorded.</summary>
    HistoryOnly,
    /// <summary>The history already has an entry with the same deduplication key.</summary>
    Duplicate,
    /// <summary>The saga has completed, so the report was dropped.</summary>
    Ignored
}

public static class SagaTimeoutRules
{
    /// <summary>Absorbs delayed-delivery latency and clock drift before a pending timeout counts as overdue.</summary>
    public static readonly TimeSpan OverdueGracePeriod = TimeSpan.FromMinutes(5);

    /// <summary>A pending operator request older than this no longer blocks a new one.</summary>
    public static readonly TimeSpan OperatorRequestTimeout = TimeSpan.FromMinutes(10);

    public const int MaxHistoryEntries = 100;

    public static SagaTimeoutStatus StatusAfter(SagaTimeoutChangeKind kind) => kind switch
    {
        SagaTimeoutChangeKind.Requested or SagaTimeoutChangeKind.Rescheduled => SagaTimeoutStatus.Pending,
        SagaTimeoutChangeKind.Triggered => SagaTimeoutStatus.Triggered,
        SagaTimeoutChangeKind.Canceled => SagaTimeoutStatus.Canceled,
        SagaTimeoutChangeKind.Rejected => throw new ArgumentOutOfRangeException(nameof(kind), kind, "A rejection does not change the state."),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    // Terminal states win a tie on Version, because Canceled does not increment it
    public static int RankOf(SagaTimeoutChangeKind kind) => kind is SagaTimeoutChangeKind.Triggered or SagaTimeoutChangeKind.Canceled ? 1 : 0;

    public static string DeduplicationKey(SagaTimeoutChange change) => change.Kind == SagaTimeoutChangeKind.Rejected
        ? $"Rejected:{change.RequestedChange}:{change.ChangedAt.UtcTicks}"
        : $"{change.Kind}:{change.Version}";

    public static bool IsOverdue(SagaTimeoutStatus status, DateTime? dueTime, DateTime now) =>
        status == SagaTimeoutStatus.Pending && dueTime < now - OverdueGracePeriod;
}

/// <summary>
/// One change reported by an endpoint. <see cref="ChangedAt"/> comes from the endpoint clock and <see cref="ReceivedAt"/> from ServiceControl.
/// </summary>
public sealed record SagaTimeoutChange
{
    public required SagaTimeoutChangeKind Kind { get; init; }
    public required string TimeoutId { get; init; }
    public required string EndpointName { get; init; }
    public required string SagaType { get; init; }
    public required Guid SagaId { get; init; }
    public string? CorrelationValue { get; init; }

    // Not reported by a rejection
    public string? TimeoutType { get; init; }
    public int Version { get; init; }

    public string? TriggerId { get; init; }
    public DateTimeOffset? DueTime { get; init; }
    public string? PayloadContentType { get; init; }
    public byte[]? Payload { get; init; }

    public Guid? OperationId { get; init; }

    public string? RequestedChange { get; init; }
    public DateTimeOffset? RequestedDueTime { get; init; }
    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
    public required DateTimeOffset ReceivedAt { get; init; }
    public string? Host { get; init; }
    public string? HostId { get; init; }
    public string? ManagementAddress { get; init; }

    public SagaTimeoutChangeSource Source => OperationId.HasValue ? SagaTimeoutChangeSource.Operator : SagaTimeoutChangeSource.Saga;
}

public sealed record SagaCompletedChange(string EndpointName, string SagaType, Guid SagaId, DateTimeOffset ChangedAt, DateTimeOffset ReceivedAt);

public enum SagaTimeoutListStatus
{
    /// <summary>Pending and not overdue.</summary>
    Pending,
    Overdue,
    /// <summary>Pending and finished timeouts, without the Unknown ones.</summary>
    All
}

public sealed record SagaTimeoutQuery
{
    public SagaTimeoutListStatus Status { get; init; } = SagaTimeoutListStatus.Pending;
    public string? EndpointName { get; init; }
    public string? SagaType { get; init; }
    public Guid? SagaId { get; init; }
    public string? SearchText { get; init; }

    public static readonly string[] SortOptions = ["due_time", "last_changed_at", "endpoint", "saga_type", "timeout_type"];
}

public sealed record SagaTimeoutOperatorRequest(
    string TimeoutId,
    Guid OperationId,
    SagaTimeoutOperation Change,
    int ExpectedVersion,
    DateTimeOffset? RequestedDueTime,
    DateTimeOffset RequestedAt);

public enum SagaTimeoutOperatorRequestOutcome
{
    Accepted,
    NotFound,
    VersionMismatch,
    OperationPending,
    NotPending,
    NoManagementAddress
}

/// <param name="Timeout">The timeout after the request was recorded; null when it does not exist.</param>
public sealed record SagaTimeoutOperatorRequestResult(SagaTimeoutOperatorRequestOutcome Outcome, SagaTimeoutView? Timeout = null);
