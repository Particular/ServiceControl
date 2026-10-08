namespace ServiceControl.UnitTests.Notifications.Webhooks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using ServiceControl.Notifications.Webhooks;
    using ServiceControl.Recoverability.ExternalIntegration;

    [TestFixture]
    public class AlertmanagerAlertFactoryTests
    {
        [SetUp]
        public void SetUp()
        {
            store = new StubFailedMessageStore();
            settings = WebhookTestData.CreateSettings();
        }

        AlertmanagerAlertFactory CreateFactory() => new(store, settings, WebhookTestData.CreateTimeProvider());

        Task<IReadOnlyList<AlertmanagerAlert>> CreateAlerts(params object[] events) => CreateFactory().CreateAlerts(events);

        [Test]
        public async Task Message_failed_fires_an_error_alert()
        {
            var failedMessage = WebhookTestData.FailedMessage(FailedMessageStatus.Unresolved);

            var alert = (await CreateAlerts(failedMessage.ToEvent())).Single();

            Assert.Multiple(() =>
            {
                Assert.That(alert.Labels, Is.EquivalentTo(new Dictionary<string, string>
                {
                    ["alertname"] = "MessageFailed",
                    ["severity"] = "error",
                    ["servicecontrol_instance"] = "Particular.ServiceControl",
                    ["endpoint"] = "Sales",
                    ["message_type"] = "Sales.Messages.PlaceOrder",
                    ["failed_message_id"] = failedMessage.UniqueMessageId
                }));
                Assert.That(alert.Annotations["summary"], Is.EqualTo("Failed to process message 'Sales.Messages.PlaceOrder' in endpoint 'Sales'"));
                Assert.That(alert.Annotations["description"], Is.EqualTo("System.InvalidOperationException: Order total is negative"));
                Assert.That(alert.Annotations["exception_type"], Is.EqualTo("System.InvalidOperationException"));
                Assert.That(alert.Annotations["exception_message"], Is.EqualTo("Order total is negative"));
                Assert.That(alert.Annotations["failing_address"], Is.EqualTo("Sales@worker-01"));
                Assert.That(alert.Annotations["host"], Is.EqualTo("worker-01"));
                Assert.That(alert.Annotations["sending_endpoint"], Is.EqualTo("Sales.Api"));
                Assert.That(alert.Annotations["message_id"], Is.EqualTo("native-message-id"));
                Assert.That(alert.Annotations["processing_attempts"], Is.EqualTo("1"));
                Assert.That(alert.Annotations["dedup_key"], Is.EqualTo(AlertmanagerAlert.ComputeDedupKey(alert.Labels)));
                Assert.That(alert.Annotations["servicepulse_url"], Is.EqualTo($"https://servicepulse.example.com/#/failed-messages/message/{failedMessage.UniqueMessageId}"));
                Assert.That(alert.GeneratorUrl, Is.EqualTo(alert.Annotations["servicepulse_url"]));
                Assert.That(alert.StartsAt, Is.EqualTo(WebhookTestData.TimeOfFailure));
                Assert.That(alert.EndsAt, Is.Null);
                Assert.That(alert.IsResolved, Is.False);
            });
        }

        [Test]
        public async Task Archived_failures_do_not_fire()
        {
            var archived = WebhookTestData.FailedMessage(FailedMessageStatus.Archived).ToEvent();

            Assert.That(await CreateAlerts(archived), Is.Empty);
        }

        [Test]
        public async Task Links_are_omitted_when_servicepulse_url_is_not_configured()
        {
            settings = WebhookTestData.CreateSettings(servicePulseUrl: null);

            var alert = (await CreateAlerts(WebhookTestData.FailedMessage(FailedMessageStatus.Unresolved).ToEvent())).Single();

            Assert.That(alert.GeneratorUrl, Is.Null);
            Assert.That(alert.Annotations, Does.Not.ContainKey("servicepulse_url"));
        }

        [Test]
        public async Task Long_exception_messages_are_truncated()
        {
            var failedMessage = WebhookTestData.FailedMessage(FailedMessageStatus.Unresolved);
            failedMessage.ProcessingAttempts[0].FailureDetails.Exception.Message = new string('x', 5000);

            var alert = (await CreateAlerts(failedMessage.ToEvent())).Single();

            Assert.That(alert.Annotations["exception_message"], Has.Length.EqualTo(1000).And.EndsWith("…"));
        }

        static IEnumerable<TestCaseData> ResolutionEvents()
        {
            yield return new TestCaseData((Func<string, object>)(id => new Contracts.MessageFailureResolvedByRetry { FailedMessageId = id }), FailedMessageStatus.Resolved).SetArgDisplayNames("resolved by retry");
            yield return new TestCaseData((Func<string, object>)(id => new Contracts.MessageFailureResolvedManually { FailedMessageId = id }), FailedMessageStatus.Resolved).SetArgDisplayNames("resolved manually");
            yield return new TestCaseData((Func<string, object>)(id => new Contracts.MessageEditedAndRetried { FailedMessageId = id }), FailedMessageStatus.Resolved).SetArgDisplayNames("edited and retried");
            yield return new TestCaseData((Func<string, object>)(id => new Contracts.FailedMessagesArchived { FailedMessagesIds = [id] }), FailedMessageStatus.Archived).SetArgDisplayNames("archived");
        }

        [TestCaseSource(nameof(ResolutionEvents))]
        public async Task Resolution_events_resolve_the_alert_with_the_same_labels(Func<string, object> createEvent, FailedMessageStatus currentStatus)
        {
            var unresolved = WebhookTestData.FailedMessage(FailedMessageStatus.Unresolved);
            var firing = (await CreateAlerts(unresolved.ToEvent())).Single();
            unresolved.Status = currentStatus;
            store.Add(unresolved);

            var resolved = (await CreateAlerts(createEvent(unresolved.UniqueMessageId))).Single();

            Assert.Multiple(() =>
            {
                Assert.That(resolved.Labels, Is.EquivalentTo(firing.Labels));
                Assert.That(resolved.Fingerprint, Is.EqualTo(firing.Fingerprint));
                Assert.That(resolved.Annotations["dedup_key"], Is.EqualTo(firing.Annotations["dedup_key"]));
                Assert.That(resolved.Annotations["summary"], Does.EndWith("has been resolved"));
                Assert.That(resolved.StartsAt, Is.EqualTo(firing.StartsAt));
                Assert.That(resolved.EndsAt, Is.EqualTo(WebhookTestData.Now));
                Assert.That(resolved.IsResolved, Is.True);
            });
        }

        [TestCase(FailedMessageStatus.Unresolved)]
        [TestCase(FailedMessageStatus.RetryIssued)]
        public async Task Stale_resolution_events_are_ignored(FailedMessageStatus currentStatus)
        {
            // e.g. the message was resolved, but has failed again or is being retried again before the event was dispatched
            var failedMessage = store.Add(WebhookTestData.FailedMessage(currentStatus)).Messages.Values.Single();

            var alerts = await CreateAlerts(new Contracts.MessageFailureResolvedByRetry { FailedMessageId = failedMessage.UniqueMessageId });

            Assert.That(alerts, Is.Empty);
        }

        [Test]
        public async Task Resolution_events_for_unknown_messages_are_ignored()
        {
            var alerts = await CreateAlerts(
                new Contracts.MessageFailureResolvedManually { FailedMessageId = Guid.NewGuid().ToString() },
                new Contracts.MessageFailureResolvedManually { FailedMessageId = "not-a-guid" });

            Assert.That(alerts, Is.Empty);
        }

        [Test]
        public async Task Archiving_resolves_all_archived_messages_with_a_single_lookup()
        {
            var messages = Enumerable.Range(0, 3).Select(_ => WebhookTestData.FailedMessage(FailedMessageStatus.Archived)).ToArray();
            store.Add(messages);

            var alerts = await CreateAlerts(new Contracts.FailedMessagesArchived { FailedMessagesIds = [.. messages.Select(m => m.UniqueMessageId)] });

            Assert.That(alerts.Select(a => a.Labels["failed_message_id"]), Is.EquivalentTo(messages.Select(m => m.UniqueMessageId)));
            Assert.That(alerts.All(a => a.IsResolved), Is.True);
            Assert.That(store.Lookups, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Lookups_are_batched()
        {
            var ids = Enumerable.Range(0, 600).Select(_ => Guid.NewGuid().ToString()).ToArray();

            await CreateAlerts(new Contracts.FailedMessagesArchived { FailedMessagesIds = ids });

            Assert.That(store.Lookups.Select(l => l.Length), Is.EqualTo(new[] { 256, 256, 88 }));
        }

        [Test]
        public void Lookup_failures_propagate_so_the_batch_is_retried()
        {
            store.LookupException = new InvalidOperationException("database unavailable");

            Assert.ThrowsAsync<InvalidOperationException>(() => CreateAlerts(new Contracts.MessageFailureResolvedManually { FailedMessageId = Guid.NewGuid().ToString() }));
        }

        [TestCase(FailedMessageStatus.Unresolved, true)]
        [TestCase(FailedMessageStatus.Archived, false)]
        [TestCase(FailedMessageStatus.Resolved, false)]
        public async Task Unarchiving_fires_the_alert_again_if_the_message_is_unresolved(FailedMessageStatus currentStatus, bool fires)
        {
            var failedMessage = store.Add(WebhookTestData.FailedMessage(currentStatus)).Messages.Values.Single();

            var alerts = await CreateAlerts(new Contracts.FailedMessagesUnArchived { FailedMessagesIds = [failedMessage.UniqueMessageId] });

            Assert.That(alerts.Count(a => !a.IsResolved), Is.EqualTo(fires ? 1 : 0));
            Assert.That(alerts, Has.Count.EqualTo(fires ? 1 : 0));
        }

        [Test]
        public async Task Heartbeat_stopped_fires_and_heartbeat_restored_resolves()
        {
            var hostId = Guid.Parse("8a2a3ad8-1d43-4c2b-a4b3-6c3a2b9e2f10");
            var detectedAt = WebhookTestData.Now.AddMinutes(-2);
            var lastReceivedAt = WebhookTestData.Now.AddMinutes(-3);

            var stopped = (await CreateAlerts(new Contracts.HeartbeatStopped { EndpointName = "Billing Service", Host = "worker-02", HostId = hostId, DetectedAt = detectedAt, LastReceivedAt = lastReceivedAt })).Single();
            var restored = (await CreateAlerts(new Contracts.HeartbeatRestored { EndpointName = "Billing Service", Host = "worker-02", HostId = hostId, RestoredAt = WebhookTestData.Now })).Single();

            Assert.Multiple(() =>
            {
                Assert.That(stopped.Labels, Is.EquivalentTo(new Dictionary<string, string>
                {
                    ["alertname"] = "HeartbeatStopped",
                    ["severity"] = "critical",
                    ["servicecontrol_instance"] = "Particular.ServiceControl",
                    ["endpoint"] = "Billing Service",
                    ["host"] = "worker-02",
                    ["host_id"] = hostId.ToString()
                }));
                Assert.That(stopped.StartsAt, Is.EqualTo(detectedAt));
                Assert.That(stopped.EndsAt, Is.Null);
                Assert.That(stopped.Annotations["last_heartbeat_at"], Is.EqualTo("2025-03-04T10:27:00.0000000Z"));
                Assert.That(stopped.GeneratorUrl, Is.EqualTo("https://servicepulse.example.com/#/heartbeats/instances/Billing%20Service"));

                Assert.That(restored.Labels, Is.EquivalentTo(stopped.Labels));
                Assert.That(restored.Annotations["dedup_key"], Is.EqualTo(stopped.Annotations["dedup_key"]));
                Assert.That(restored.EndsAt, Is.EqualTo(WebhookTestData.Now));
            });
        }

        [Test]
        public async Task Custom_check_failed_fires_and_custom_check_succeeded_resolves()
        {
            var hostId = Guid.NewGuid();

            var failed = (await CreateAlerts(new Contracts.CustomCheckFailed { EndpointName = "Sales", Host = "worker-01", HostId = hostId, CustomCheckId = "SqlServer", Category = "Database", FailureReason = "Connection refused", FailedAt = WebhookTestData.TimeOfFailure })).Single();
            var succeeded = (await CreateAlerts(new Contracts.CustomCheckSucceeded { EndpointName = "Sales", Host = "worker-01", HostId = hostId, CustomCheckId = "SqlServer", Category = "Database", SucceededAt = WebhookTestData.Now })).Single();

            Assert.Multiple(() =>
            {
                Assert.That(failed.Labels["alertname"], Is.EqualTo("CustomCheckFailed"));
                Assert.That(failed.Labels["severity"], Is.EqualTo("warning"));
                Assert.That(failed.Labels["custom_check_id"], Is.EqualTo("SqlServer"));
                Assert.That(failed.Labels["category"], Is.EqualTo("Database"));
                Assert.That(failed.Annotations["failure_reason"], Is.EqualTo("Connection refused"));
                Assert.That(failed.StartsAt, Is.EqualTo(WebhookTestData.TimeOfFailure));
                Assert.That(failed.GeneratorUrl, Is.EqualTo("https://servicepulse.example.com/#/custom-checks"));

                Assert.That(succeeded.Labels, Is.EquivalentTo(failed.Labels));
                Assert.That(succeeded.EndsAt, Is.EqualTo(WebhookTestData.Now));
                Assert.That(succeeded.StartsAt, Is.Null);
            });
        }

        [Test]
        public async Task Empty_label_values_are_omitted()
        {
            var alert = (await CreateAlerts(new Contracts.CustomCheckFailed { EndpointName = "Sales", Host = "worker-01", HostId = Guid.NewGuid(), CustomCheckId = "SqlServer", Category = null, FailureReason = "x", FailedAt = WebhookTestData.Now })).Single();

            Assert.That(alert.Labels, Does.Not.ContainKey("category"));
        }

        [Test]
        public async Task Only_the_latest_state_of_an_alert_within_a_batch_is_kept()
        {
            var hostId = Guid.NewGuid();

            var alerts = await CreateAlerts(
                new Contracts.HeartbeatStopped { EndpointName = "Sales", Host = "worker-01", HostId = hostId, DetectedAt = WebhookTestData.TimeOfFailure, LastReceivedAt = WebhookTestData.TimeOfFailure },
                new Contracts.HeartbeatStopped { EndpointName = "Billing", Host = "worker-01", HostId = hostId, DetectedAt = WebhookTestData.TimeOfFailure, LastReceivedAt = WebhookTestData.TimeOfFailure },
                new Contracts.HeartbeatRestored { EndpointName = "Sales", Host = "worker-01", HostId = hostId, RestoredAt = WebhookTestData.Now });

            Assert.That(alerts.Select(a => (a.Labels["endpoint"], a.IsResolved)), Is.EqualTo(new[] { ("Billing", false), ("Sales", true) }));
        }

        [Test]
        public async Task Unrelated_events_are_ignored() =>
            Assert.That(await CreateAlerts(new object(), "unrelated"), Is.Empty);

        StubFailedMessageStore store;
        ServiceBus.Management.Infrastructure.Settings.Settings settings;
    }
}
