namespace ServiceControl.Notifications.Webhooks
{
    using System.Threading.Tasks;
    using NServiceBus;

    [Handler]
    class SendWebhookNotificationHandler(WebhookSender sender) : IHandleMessages<SendWebhookNotification>
    {
        public Task Handle(SendWebhookNotification message, IMessageHandlerContext context) =>
            sender.Send(message.WebhookName, message.Alerts ?? [], context.CancellationToken);
    }
}
