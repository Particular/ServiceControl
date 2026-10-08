namespace ServiceControl.Notifications.Webhooks
{
    using NServiceBus;

    /// <summary>
    /// Delivers alerts to a single webhook. Only the webhook name is included because the target URL and headers can contain secrets.
    /// </summary>
    public class SendWebhookNotification : ICommand
    {
        public string WebhookName { get; set; }

        public AlertmanagerAlert[] Alerts { get; set; }
    }
}
