namespace ServiceControl.Notifications
{
    public class NotificationsSettings
    {
        public const string SingleDocumentId = "NotificationsSettings/All";

        public string Id { get; set; }

        public EmailNotifications Email { get; set; } = new EmailNotifications();
    }
}