namespace ServiceControl.Persistence.EFCore.Infrastructure;

/// <summary>
/// A retry acknowledgement, carrying the time the retry succeeded so that a message which failed
/// again afterwards is not resolved by it.
/// </summary>
public readonly record struct ConfirmedRetry(Guid UniqueMessageId, DateTime SucceededAt);