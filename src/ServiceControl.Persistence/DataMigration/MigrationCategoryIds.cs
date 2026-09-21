namespace ServiceControl.Persistence.DataMigration;

/// <summary>
/// The name of every category a migration can copy. The name is what the checkpoint row is keyed on and what an
/// operator writes in <see cref="MigrationSettings.OptionalCategoriesKey" />, so renaming one strands the progress
/// already saved under the old name. <see cref="MigrationCategoryRegistry" /> says when each one is copied.
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
    public const string UnresolvedAndRetryIssuedFailedMessages = nameof(UnresolvedAndRetryIssuedFailedMessages);

    public const string EventLog = nameof(EventLog);
    public const string CustomChecks = nameof(CustomChecks);
    public const string FailedErrorImports = nameof(FailedErrorImports);
    public const string FailedMessageEdits = nameof(FailedMessageEdits);
    public const string ArchivedAndResolvedFailedMessages = nameof(ArchivedAndResolvedFailedMessages);
    public const string GroupComments = nameof(GroupComments);
}
