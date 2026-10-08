namespace ServiceControl.Notifications.Webhooks
{
    using EventLog;
    using Infrastructure.DomainEvents;

    class WebhookNotificationFailed : IDomainEvent
    {
        public string WebhookName { get; set; }
        public string Reason { get; set; }
        public int AlertCount { get; set; }
    }

    class WebhookNotificationFailedDefinition : EventLogMappingDefinition<WebhookNotificationFailed>
    {
        public WebhookNotificationFailedDefinition()
        {
            Description(m => $"Webhook notification '{m.WebhookName}' with {m.AlertCount} alert(s) could not be delivered. Reason: {m.Reason}");
            TreatAsError();
        }
    }
}
