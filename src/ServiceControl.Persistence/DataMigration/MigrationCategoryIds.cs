namespace ServiceControl.Persistence.DataMigration;

/// <summary>
/// The name of every category a migration can copy. The name is what the checkpoint row is keyed on, so renaming
/// one strands the progress already saved under the old name. <see cref="MigrationCategoryRegistry" /> says when each one is copied.
/// </summary>
public static class MigrationCategoryIds
{
    public const string KnownEndpoints = nameof(KnownEndpoints);
    public const string EndpointSettings = nameof(EndpointSettings);
    public const string MessageRedirects = nameof(MessageRedirects);
    public const string Subscriptions = nameof(Subscriptions);
    public const string NotificationSettings = nameof(NotificationSettings);
    public const string TrialEndDate = nameof(TrialEndDate);
    public const string RetryOperations = nameof(RetryOperations);
    public const string LicensingEndpoints = nameof(LicensingEndpoints);
    public const string LicensingThroughput = nameof(LicensingThroughput);
    public const string LicensingReportMasks = nameof(LicensingReportMasks);
    public const string LicensedEndpointDetails = nameof(LicensedEndpointDetails);

    /// <summary>
    /// Recent errors of any status, plus retry-issued messages and retry-claim holders of any age, copied before the host opens.
    /// </summary>
    public const string RecentAndRetriedFailedMessages = nameof(RecentAndRetriedFailedMessages);

    /// <summary>The unresolved failed messages, copied once the host is open, whatever their age.</summary>
    public const string UnresolvedFailedMessages = nameof(UnresolvedFailedMessages);
    public const string PendingIntegrationEvents = nameof(PendingIntegrationEvents);

    public const string EventLog = nameof(EventLog);
    public const string CustomChecks = nameof(CustomChecks);
    public const string FailedErrorImports = nameof(FailedErrorImports);
    public const string ArchivedAndResolvedFailedMessages = nameof(ArchivedAndResolvedFailedMessages);
    public const string GroupComments = nameof(GroupComments);
}
