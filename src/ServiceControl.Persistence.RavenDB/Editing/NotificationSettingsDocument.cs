namespace ServiceControl.Persistence.RavenDB.Editing;

using Notifications;

class NotificationsSettingsDocument
{
    public string Id { get; set; }
    public EmailNotifications Email { get; set; } = new();
}