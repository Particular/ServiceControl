namespace ServiceControl.Persistence.DataMigration;

using ServiceControl.Recoverability;

/// <summary>
/// One retry operation from the retry history document. Exactly one of Historic and Unacknowledged is set.
/// </summary>
public sealed record RetryOperationRow(HistoricRetryOperation? Historic, UnacknowledgedRetryOperation? Unacknowledged);
