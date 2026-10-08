namespace ServiceControl.UnitTests.Notifications.Webhooks
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging.Abstractions;
    using NServiceBus;
    using NServiceBus.Testing;
    using NUnit.Framework;
    using ServiceControl.Notifications.Webhooks;
    using ServiceControl.UnitTests.Operations;

    [TestFixture]
    public class WebhookIntegrationEventSinkTests
    {
        [SetUp]
        public void SetUp()
        {
            session = new TestableMessageSession();
            domainEvents = new FakeDomainEvents();
        }

        WebhookIntegrationEventSink CreateSink(string webhooksJson, IMessageSession messageSession = null)
        {
            var settings = WebhookTestData.CreateSettings(webhooksJson);
            return new WebhookIntegrationEventSink(
                new AlertmanagerAlertFactory(new StubFailedMessageStore(), settings, WebhookTestData.CreateTimeProvider()),
                settings,
                new WebhookDeliveryOptions { MaxAlertsPerNotification = 2 },
                messageSession ?? session,
                domainEvents,
                NullLogger<WebhookIntegrationEventSink>.Instance);
        }

        static object HeartbeatStopped(string endpoint) =>
            new Contracts.HeartbeatStopped { EndpointName = endpoint, Host = "worker-01", HostId = Guid.NewGuid(), DetectedAt = WebhookTestData.Now, LastReceivedAt = WebhookTestData.Now };

        static object CustomCheckFailed(string endpoint) =>
            new Contracts.CustomCheckFailed { EndpointName = endpoint, Host = "worker-01", HostId = Guid.NewGuid(), CustomCheckId = "check", FailureReason = "boom", FailedAt = WebhookTestData.Now };

        SendWebhookNotification[] Sent(string webhookName = null) =>
        [
            .. session.SentMessages
                .Select(m => (SendWebhookNotification)m.Message)
                .Where(m => webhookName == null || m.WebhookName == webhookName)
        ];

        [Test]
        public async Task Sends_a_notification_per_webhook_to_this_endpoint()
        {
            var sink = CreateSink("""[ { "Name": "a", "Url": "https://a.example.com" }, { "Name": "b", "Url": "https://b.example.com" } ]""");

            await sink.Dispatch([HeartbeatStopped("Sales")]);

            Assert.That(Sent().Select(m => m.WebhookName), Is.EquivalentTo(new[] { "a", "b" }));
            Assert.That(session.SentMessages.All(m => m.Options.IsRoutingToThisEndpoint()), Is.True);
            Assert.That(Sent().All(m => m.Alerts.Single().Labels["endpoint"] == "Sales"), Is.True);
        }

        [Test]
        public async Task Only_alert_types_selected_for_a_webhook_are_sent_to_it()
        {
            var sink = CreateSink("""
                [
                  { "Name": "heartbeats", "Url": "https://a.example.com", "Alerts": [ "HeartbeatStopped" ] },
                  { "Name": "everything", "Url": "https://b.example.com" }
                ]
                """);

            await sink.Dispatch([HeartbeatStopped("Sales"), CustomCheckFailed("Sales")]);

            Assert.That(Sent("heartbeats").SelectMany(m => m.Alerts).Select(a => a.AlertName), Is.EqualTo(new[] { "HeartbeatStopped" }));
            Assert.That(Sent("everything").SelectMany(m => m.Alerts).Select(a => a.AlertName), Is.EquivalentTo(new[] { "HeartbeatStopped", "CustomCheckFailed" }));
        }

        [Test]
        public async Task Alerts_are_batched_for_default_payloads()
        {
            var sink = CreateSink("""[ { "Url": "https://a.example.com" } ]""");

            await sink.Dispatch([HeartbeatStopped("A"), HeartbeatStopped("B"), HeartbeatStopped("C")]);

            Assert.That(Sent().Select(m => m.Alerts.Length), Is.EqualTo(new[] { 2, 1 }));
        }

        [Test]
        public async Task Each_alert_is_sent_separately_when_a_template_is_used()
        {
            var sink = CreateSink("""[ { "Url": "https://a.example.com", "Template": { "a": "#valueof($[0].labels.endpoint)" } } ]""");

            await sink.Dispatch([HeartbeatStopped("A"), HeartbeatStopped("B"), HeartbeatStopped("C")]);

            Assert.That(Sent().Select(m => m.Alerts.Length), Is.EqualTo(new[] { 1, 1, 1 }));
        }

        [Test]
        public async Task Nothing_is_sent_when_there_are_no_alerts()
        {
            var sink = CreateSink("""[ { "Url": "https://a.example.com", "Alerts": [ "MessageFailed" ] } ]""");

            await sink.Dispatch([HeartbeatStopped("A"), new object()]);

            Assert.That(session.SentMessages, Is.Empty);
        }

        [Test]
        public async Task A_failure_to_queue_a_notification_is_recorded_and_does_not_affect_other_webhooks()
        {
            var failingSession = new FailingMessageSession("a");
            var sink = CreateSink("""[ { "Name": "a", "Url": "https://a.example.com" }, { "Name": "b", "Url": "https://b.example.com" } ]""", failingSession);

            await sink.Dispatch([HeartbeatStopped("Sales")]);

            Assert.That(failingSession.SentMessages.Select(m => ((SendWebhookNotification)m.Message).WebhookName), Is.EqualTo(new[] { "b" }));
            var failure = domainEvents.RaisedEvents.OfType<WebhookNotificationFailed>().Single();
            Assert.That(failure.WebhookName, Is.EqualTo("a"));
            Assert.That(failure.AlertCount, Is.EqualTo(1));
            Assert.That(failure.Reason, Does.Contain("transport unavailable"));
        }

        [Test]
        public void Cancellation_is_not_swallowed()
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();
            var sink = CreateSink("""[ { "Url": "https://a.example.com" } ]""", new CancelledMessageSession());

            Assert.CatchAsync<OperationCanceledException>(() => sink.Dispatch([HeartbeatStopped("Sales")], cancellationTokenSource.Token));
        }

        class FailingMessageSession(string failingWebhook) : TestableMessageSession
        {
            public override Task Send(object message, SendOptions sendOptions, CancellationToken cancellationToken = default) =>
                ((SendWebhookNotification)message).WebhookName == failingWebhook
                    ? throw new InvalidOperationException("transport unavailable")
                    : base.Send(message, sendOptions, cancellationToken);
        }

        class CancelledMessageSession : TestableMessageSession
        {
            public override Task Send(object message, SendOptions sendOptions, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return base.Send(message, sendOptions, cancellationToken);
            }
        }

        TestableMessageSession session;
        FakeDomainEvents domainEvents;
    }
}
