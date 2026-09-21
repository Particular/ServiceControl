namespace ServiceControl.Persistence.EFCore.DataMigration;

using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Refuses a copy that the instance's own retry history setting would throw away. At a depth of zero the
/// persister empties the history table whenever a retry completes, and the migrated rows would go with it.
/// </summary>
sealed class RetryHistoryDepthIsSafeCheck(int retryHistoryDepth) : IMigrationStartupCheck
{
    public string Name => "the retry history depth will not empty a migrated table";

    public Task Run(CancellationToken cancellationToken = default)
    {
        // The depth at which RetryHistoryDataStore.TrimHistory deletes every row rather than trimming.
        if (retryHistoryDepth <= 0)
        {
            throw new Exception(
                $"ServiceControl/RetryHistoryDepth is {retryHistoryDepth}, and at that depth this persister deletes every row of HistoricRetryOperations when a retry completes. The RetryOperations category copies that table, so the first completed retry after cutover would silently discard the migrated retry history. Set ServiceControl/RetryHistoryDepth to a positive number, or clear it to use the default of 10, before setting {MigrationSettings.EnabledKey}.");
        }

        return Task.CompletedTask;
    }
}
