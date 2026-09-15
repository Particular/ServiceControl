namespace ServiceControl.Persistence.DataMigration;

using System.Collections.Generic;
using System.Linq;
using ServiceControl.MessageFailures;

public static class MigrationCategoryRegistry
{
    public static readonly IReadOnlyList<FailedMessageStatus> UnresolvedAndRetryIssuedStatuses = [FailedMessageStatus.Unresolved, FailedMessageStatus.RetryIssued];
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
        new(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages, MigrationCategoryKind.Required, CarriesBodies: true, Order: 12),

        // Optional, copied in the background once the host is open.
        new(MigrationCategoryIds.EventLog, MigrationCategoryKind.Optional, CarriesBodies: false, Order: 1),
        new(MigrationCategoryIds.CustomChecks, MigrationCategoryKind.Optional, CarriesBodies: false, Order: 2),
        new(MigrationCategoryIds.FailedErrorImports, MigrationCategoryKind.Optional, CarriesBodies: true, Order: 3),
        new(MigrationCategoryIds.FailedMessageEdits, MigrationCategoryKind.Optional, CarriesBodies: false, Order: 4),
        new(MigrationCategoryIds.ArchivedAndResolvedFailedMessages, MigrationCategoryKind.Optional, CarriesBodies: true, Order: 5),
        new(MigrationCategoryIds.GroupComments, MigrationCategoryKind.Optional, CarriesBodies: false, Order: 6, MustFollow: MigrationCategoryIds.ArchivedAndResolvedFailedMessages),
    ];

    public static MigrationCategory? Find(string id) => All.FirstOrDefault(c => c.Id == id);
}
