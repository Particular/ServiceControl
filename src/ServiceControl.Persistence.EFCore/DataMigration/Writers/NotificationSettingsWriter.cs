namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using Infrastructure;
using ServiceControl.Notifications;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Writes the notification email settings as one Settings row.
/// </summary>
sealed class NotificationSettingsWriter(IMigrationSqlDialect migrationDialect) : SettingRowWriter<EmailNotifications>(migrationDialect, SettingKeys.NotificationEmails)
{
    public override string CategoryId => MigrationCategoryIds.NotificationSettings;
}
