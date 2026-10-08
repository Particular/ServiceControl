namespace ServiceControl.Notifications.Webhooks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using ExternalIntegrations;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Settings;

    /// <summary>
    /// Turns outbox integration events into alerts and queues one <see cref="SendWebhookNotification"/> per webhook,
    /// so that slow or failing HTTP targets never block or fail the outbox dispatch.
    /// </summary>
    class WebhookIntegrationEventSink(
        AlertmanagerAlertFactory alertFactory,
        Settings settings,
        WebhookDeliveryOptions options,
        IMessageSession messageSession,
        IDomainEvents domainEvents,
        ILogger<WebhookIntegrationEventSink> logger) : IIntegrationEventSink
    {
        public async Task Dispatch(IReadOnlyCollection<object> integrationEvents, CancellationToken cancellationToken = default)
        {
            var alerts = await alertFactory.CreateAlerts(integrationEvents, cancellationToken);
            if (alerts.Count == 0)
            {
                return;
            }

            foreach (var webhook in settings.Webhooks)
            {
                var selected = alerts
                    .Where(alert => Enum.TryParse<WebhookAlertType>(alert.AlertName, out var alertType) && webhook.Accepts(alertType))
                    .ToArray();

                // Templates are typically written for targets that accept a single event per request (e.g. PagerDuty)
                var chunkSize = webhook.HasTemplate ? 1 : Math.Max(1, options.MaxAlertsPerNotification);

                foreach (var chunk in selected.Chunk(chunkSize))
                {
                    await Enqueue(webhook, chunk, cancellationToken);
                }
            }
        }

        // Mirrors how the outbox handles bus publishing failures: throwing would make the outbox retry the whole batch,
        // republishing integration events that bus subscribers have already received.
        async Task Enqueue(WebhookTarget webhook, AlertmanagerAlert[] alerts, CancellationToken cancellationToken)
        {
            try
            {
                var sendOptions = new SendOptions();
                sendOptions.RouteToThisEndpoint();
                await messageSession.Send(new SendWebhookNotification { WebhookName = webhook.Name, Alerts = alerts }, sendOptions, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to queue webhook notification {WebhookName} for alerts {AlertNames}", webhook.Name, string.Join(", ", alerts.Select(a => a.AlertName)));

                await domainEvents.Raise(new WebhookNotificationFailed
                {
                    WebhookName = webhook.Name,
                    AlertCount = alerts.Length,
                    Reason = $"The notification could not be queued: {e.GetBaseException().Message}"
                }, cancellationToken);
            }
        }
    }
}
