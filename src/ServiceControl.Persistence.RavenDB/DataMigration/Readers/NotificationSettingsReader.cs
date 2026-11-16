#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration.Readers;

using System.Collections.Generic;
using ServiceControl.Notifications;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.RavenDB.Editing;

/// <summary>
/// Reads the notification settings document, whose email settings become one Settings row.
/// </summary>
sealed class NotificationSettingsReader(RavenReadOnlySourceLifecycle lifecycle)
    : SingleDocumentReader<NotificationsSettingsDocument, EmailNotifications>(lifecycle, lifecycle.Settings.DatabaseName, NotificationsDataStore.SingleDocumentId)
{
    public override string CategoryId => MigrationCategoryIds.NotificationSettings;

    protected override IEnumerable<(string SourceId, EmailNotifications Document)> RowsFrom(NotificationsSettingsDocument document) =>
        document.Email is { } email ? [(DocumentId, email)] : [];
}
