namespace ServiceControl.UnitTests.Notifications.Webhooks
{
    using System;
    using System.Linq;
    using System.Text.Json.Nodes;
    using NUnit.Framework;
    using ServiceControl.Notifications.Webhooks;

    [TestFixture]
    public class AlertmanagerPayloadTests
    {
        [Test]
        public void Firing_alert_matches_the_alertmanager_v2_schema()
        {
            var alert = new AlertmanagerAlert
            {
                Labels = new() { ["severity"] = "error", ["alertname"] = "MessageFailed" },
                Annotations = new() { ["summary"] = "Something failed" },
                StartsAt = new DateTime(2025, 3, 4, 10, 0, 0, DateTimeKind.Utc),
                GeneratorUrl = "https://servicepulse.example.com/#/failed-messages/message/1"
            };

            var json = AlertmanagerPayload.Serialize([alert]);

            Assert.That(json, Is.EqualTo(
                """[{"labels":{"alertname":"MessageFailed","severity":"error"},"annotations":{"summary":"Something failed"},"startsAt":"2025-03-04T10:00:00Z","generatorURL":"https://servicepulse.example.com/#/failed-messages/message/1"}]"""));
        }

        [Test]
        public void Resolved_alert_contains_endsAt()
        {
            var alert = new AlertmanagerAlert
            {
                Labels = new() { ["alertname"] = "HeartbeatStopped" },
                EndsAt = new DateTime(2025, 3, 4, 10, 5, 0, DateTimeKind.Utc)
            };

            var json = AlertmanagerPayload.Serialize([alert]);

            Assert.That(json, Is.EqualTo("""[{"labels":{"alertname":"HeartbeatStopped"},"annotations":{},"endsAt":"2025-03-04T10:05:00Z"}]"""));
        }

        [Test]
        public void Timestamps_without_kind_are_treated_as_utc()
        {
            var alert = new AlertmanagerAlert { StartsAt = new DateTime(2025, 3, 4, 10, 0, 0, DateTimeKind.Unspecified) };

            var payload = JsonNode.Parse(AlertmanagerPayload.Serialize([alert]));

            Assert.That(payload[0]["startsAt"].GetValue<string>(), Is.EqualTo("2025-03-04T10:00:00Z"));
        }

        [Test]
        public void Multiple_alerts_are_serialized_as_one_array()
        {
            var payload = JsonNode.Parse(AlertmanagerPayload.Serialize([new AlertmanagerAlert(), new AlertmanagerAlert(), new AlertmanagerAlert()]));

            Assert.That(payload.AsArray(), Has.Count.EqualTo(3));
        }

        [Test]
        public void Dedup_key_is_stable_and_independent_of_label_order()
        {
            var first = AlertmanagerAlert.ComputeDedupKey(new System.Collections.Generic.Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });
            var second = AlertmanagerAlert.ComputeDedupKey(new System.Collections.Generic.Dictionary<string, string> { ["b"] = "2", ["a"] = "1" });
            var other = AlertmanagerAlert.ComputeDedupKey(new System.Collections.Generic.Dictionary<string, string> { ["a"] = "1", ["b"] = "3" });

            Assert.Multiple(() =>
            {
                Assert.That(first, Is.EqualTo(second));
                Assert.That(first, Is.Not.EqualTo(other));
                Assert.That(first, Has.Length.EqualTo(32));
                Assert.That(first.All(Uri.IsHexDigit), Is.True);
            });
        }
    }
}
