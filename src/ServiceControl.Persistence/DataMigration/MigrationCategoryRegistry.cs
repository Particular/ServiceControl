namespace ServiceControl.Persistence.DataMigration;

using System.Collections.Generic;
using System.Linq;
using ServiceControl.MessageFailures;

/// <summary>
/// Every category a migration can copy, and when each one is copied. This list is the whole of what a migration
/// covers: a source or a target that cannot handle a category says so through its own supported ids, and the
/// engine never invents one.
/// </summary>
public static class MigrationCategoryRegistry
{
    /// <summary>
    /// The failed message status that is copied before the host opens, whether or not a retry claim is held. A retry
    /// confirmation that arrives after the cutover resolves a message only if its row is already there.
    /// </summary>
    public static readonly IReadOnlyList<FailedMessageStatus> RetryIssuedStatuses = [FailedMessageStatus.RetryIssued];

    /// <summary>
    /// The failed message status that is copied in the background without a window, because an operator is waiting on
    /// these however old they are.
    /// </summary>
    public static readonly IReadOnlyList<FailedMessageStatus> UnresolvedStatuses = [FailedMessageStatus.Unresolved];

    /// <summary>
    /// The failed message statuses that are copied in the background. These are history, so the instance is usable
    /// while they are still arriving.
    /// </summary>
    public static readonly IReadOnlyList<FailedMessageStatus> ArchivedAndResolvedStatuses = [FailedMessageStatus.Archived, FailedMessageStatus.Resolved];

    public static readonly IReadOnlyList<MigrationCategory> All =
    [
        // Required, copied with ServiceControl closed.
        new(MigrationCategoryIds.KnownEndpoints, MigrationCategoryKind.Required, CarriesBodies: false, Order: 1),
        new(MigrationCategoryIds.EndpointSettings, MigrationCategoryKind.Required, CarriesBodies: false, Order: 2, MustFollow: MigrationCategoryIds.KnownEndpoints),
        new(MigrationCategoryIds.MessageRedirects, MigrationCategoryKind.Required, CarriesBodies: false, Order: 3),
        new(MigrationCategoryIds.Subscriptions, MigrationCategoryKind.Required, CarriesBodies: false, Order: 4),
        new(MigrationCategoryIds.NotificationSettings, MigrationCategoryKind.Required, CarriesBodies: false, Order: 5),
        new(MigrationCategoryIds.TrialEndDate, MigrationCategoryKind.Required, CarriesBodies: false, Order: 6),
        new(MigrationCategoryIds.RetryOperations, MigrationCategoryKind.Required, CarriesBodies: false, Order: 7),
        new(MigrationCategoryIds.LicensingEndpoints, MigrationCategoryKind.Required, CarriesBodies: false, Order: 8),
        new(MigrationCategoryIds.LicensingThroughput, MigrationCategoryKind.Required, CarriesBodies: false, Order: 9, MustFollow: MigrationCategoryIds.LicensingEndpoints),
        new(MigrationCategoryIds.LicensingReportMasks, MigrationCategoryKind.Required, CarriesBodies: false, Order: 10),
        new(MigrationCategoryIds.LicensedEndpointDetails, MigrationCategoryKind.Required, CarriesBodies: false, Order: 11),
        new(MigrationCategoryIds.RecentAndRetriedFailedMessages, MigrationCategoryKind.Required, CarriesBodies: true, Order: 12),
        new(MigrationCategoryIds.PendingIntegrationEvents, MigrationCategoryKind.Required, CarriesBodies: false, Order: 13),
        new(MigrationCategoryIds.CustomChecks, MigrationCategoryKind.Required, CarriesBodies: false, Order: 14),
        new(MigrationCategoryIds.FailedErrorImports, MigrationCategoryKind.Required, CarriesBodies: true, Order: 15),

        // Optional, copied in the background once the host is open.
        new(MigrationCategoryIds.UnresolvedFailedMessages, MigrationCategoryKind.Optional, CarriesBodies: true, Order: 1),
        new(MigrationCategoryIds.GroupComments, MigrationCategoryKind.Optional, CarriesBodies: false, Order: 2, MustFollow: MigrationCategoryIds.UnresolvedFailedMessages),
        new(MigrationCategoryIds.EventLog, MigrationCategoryKind.Optional, CarriesBodies: false, Order: 3),
        new(MigrationCategoryIds.ArchivedAndResolvedFailedMessages, MigrationCategoryKind.Optional, CarriesBodies: true, Order: 4),
    ];

    public static MigrationCategory? Find(string id) => All.FirstOrDefault(c => c.Id == id);
}
