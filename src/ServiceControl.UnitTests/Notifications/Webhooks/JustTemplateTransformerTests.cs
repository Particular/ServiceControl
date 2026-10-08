namespace ServiceControl.UnitTests.Notifications.Webhooks
{
    using System;
    using System.IO;
    using System.Text.Json.Nodes;
    using NUnit.Framework;
    using ServiceControl.Notifications.Webhooks;

    [TestFixture]
    public class JustTemplateTransformerTests
    {
        const string PagerDutyTemplate = """
            {
              "routing_key": "R0UT1NGK3Y",
              "event_action": "#ifcondition(#exists($[0].endsAt),true,resolve,trigger)",
              "dedup_key": "#valueof($[0].annotations.dedup_key)",
              "payload": {
                "summary": "#valueof($[0].annotations.summary)",
                "source": "#valueof($[0].labels.endpoint)",
                "severity": "#valueof($[0].labels.severity)",
                "timestamp": "#valueof($[0].startsAt)",
                "custom_details": "#valueof($[0].annotations)"
              },
              "links": [ { "href": "#valueof($[0].generatorURL)", "text": "Open in ServicePulse" } ]
            }
            """;

        static string Payload(DateTime? endsAt = null, string summary = "Failed to process message 'PlaceOrder' in endpoint 'Sales'") =>
            AlertmanagerPayload.Serialize(
            [
                new AlertmanagerAlert
                {
                    Labels = new() { ["alertname"] = "MessageFailed", ["severity"] = "error", ["endpoint"] = "Sales" },
                    Annotations = new() { ["summary"] = summary, ["dedup_key"] = "0123456789abcdef0123456789abcdef" },
                    StartsAt = new DateTime(2025, 3, 4, 10, 0, 0, DateTimeKind.Utc),
                    EndsAt = endsAt,
                    GeneratorUrl = "https://servicepulse.example.com/#/failed-messages/message/1"
                }
            ]);

        [Test]
        public void Firing_alert_can_be_transformed_into_a_pagerduty_trigger_event()
        {
            var result = JsonNode.Parse(JustTemplateTransformer.Transform(PagerDutyTemplate, Payload()));

            Assert.Multiple(() =>
            {
                Assert.That(result["routing_key"].GetValue<string>(), Is.EqualTo("R0UT1NGK3Y"));
                Assert.That(result["event_action"].GetValue<string>(), Is.EqualTo("trigger"));
                Assert.That(result["dedup_key"].GetValue<string>(), Is.EqualTo("0123456789abcdef0123456789abcdef"));
                Assert.That(result["payload"]["summary"].GetValue<string>(), Is.EqualTo("Failed to process message 'PlaceOrder' in endpoint 'Sales'"));
                Assert.That(result["payload"]["source"].GetValue<string>(), Is.EqualTo("Sales"));
                Assert.That(result["payload"]["severity"].GetValue<string>(), Is.EqualTo("error"));
                Assert.That(result["payload"]["timestamp"].ToString(), Does.StartWith("2025-03-04T10:00:00"));
                Assert.That(result["payload"]["custom_details"]["dedup_key"].GetValue<string>(), Is.EqualTo("0123456789abcdef0123456789abcdef"));
                Assert.That(result["links"][0]["href"].GetValue<string>(), Is.EqualTo("https://servicepulse.example.com/#/failed-messages/message/1"), "'#' in data is preserved");
            });
        }

        [Test]
        public void Resolved_alert_can_be_transformed_into_a_pagerduty_resolve_event()
        {
            var result = JsonNode.Parse(JustTemplateTransformer.Transform(PagerDutyTemplate, Payload(endsAt: DateTime.UtcNow)));

            Assert.That(result["event_action"].GetValue<string>(), Is.EqualTo("resolve"));
        }

        [Test]
        public void Data_that_looks_like_a_template_function_is_never_evaluated()
        {
            // Exception messages and headers are controlled by whoever can send messages to an endpoint
            var marker = Path.Combine(Path.GetTempPath(), $"sc-webhook-{Guid.NewGuid():N}");
            var malicious = $"#customfunction(System.Private.CoreLib,System.IO.Directory.CreateDirectory,{marker})";

            var result = JsonNode.Parse(JustTemplateTransformer.Transform("""{ "text": "#valueof($[0].annotations.summary)", "all": "#valueof($[0].annotations)" }""", Payload(summary: malicious)));

            Assert.Multiple(() =>
            {
                Assert.That(result["text"].GetValue<string>(), Is.EqualTo(malicious));
                Assert.That(result["all"]["summary"].GetValue<string>(), Is.EqualTo(malicious));
                Assert.That(Directory.Exists(marker), Is.False);
            });
        }

        [Test]
        public void Non_ascii_characters_are_not_escaped()
        {
            var result = JustTemplateTransformer.Transform("""{ "text": "#valueof($[0].annotations.summary)" }""", Payload(summary: "Bestellung <fehlgeschlagen> – ä"));

            Assert.That(result, Is.EqualTo("""{"text":"Bestellung <fehlgeschlagen> – ä"}"""));
        }

        [Test]
        public void Template_using_customfunction_is_rejected()
        {
            var exception = Assert.Throws<WebhookTemplateException>(() => JustTemplateTransformer.Transform("""{ "x": "#customfunction(System.Private.CoreLib,System.IO.Path.GetTempPath)" }""", Payload()));

            Assert.That(exception.Message, Does.Contain("#customfunction"));
        }

        [TestCase("""{ "x": "#doesnotexist(1)" }""")]
        [TestCase("""{ "x": "#ifcondition(1)" }""")]
        public void Template_errors_are_reported_as_template_exceptions(string template)
        {
            var exception = Assert.Throws<WebhookTemplateException>(() => JustTemplateTransformer.Transform(template, Payload()));

            Assert.That(exception.Message, Does.StartWith("The template could not be applied"));
        }
    }
}
