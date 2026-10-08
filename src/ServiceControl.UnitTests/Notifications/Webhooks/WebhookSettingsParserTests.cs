namespace ServiceControl.UnitTests.Notifications.Webhooks
{
    using System;
    using System.Linq;
    using System.Text.Json;
    using NUnit.Framework;
    using ServiceControl.Notifications.Webhooks;

    [TestFixture]
    public class WebhookSettingsParserTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        [TestCase("[]")]
        public void No_configuration_means_no_webhooks(string json) => Assert.That(WebhookSettingsParser.Parse(json), Is.Empty);

        [Test]
        public void Parses_a_complete_webhook()
        {
            var webhooks = WebhookSettingsParser.Parse("""
                [
                  {
                    "Name": "pagerduty",
                    "Url": "https://events.pagerduty.com/v2/enqueue",
                    "Headers": { "Authorization": "Token token=secret" },
                    "Template": { "routing_key": "abc" },
                    "Alerts": [ "messagefailed", "HeartbeatStopped" ]
                  }
                ]
                """);

            Assert.That(webhooks, Has.Length.EqualTo(1));
            var webhook = webhooks[0];
            Assert.Multiple(() =>
            {
                Assert.That(webhook.Name, Is.EqualTo("pagerduty"));
                Assert.That(webhook.Url, Is.EqualTo(new Uri("https://events.pagerduty.com/v2/enqueue")));
                Assert.That(webhook.Headers["authorization"], Is.EqualTo("Token token=secret"), "header names are case-insensitive");
                Assert.That(webhook.Template, Is.EqualTo("""{ "routing_key": "abc" }"""));
                Assert.That(webhook.TemplateError, Is.Null);
                Assert.That(webhook.Accepts(WebhookAlertType.MessageFailed), Is.True);
                Assert.That(webhook.Accepts(WebhookAlertType.HeartbeatStopped), Is.True);
                Assert.That(webhook.Accepts(WebhookAlertType.CustomCheckFailed), Is.False);
            });
        }

        [Test]
        public void Defaults_are_applied()
        {
            var webhooks = WebhookSettingsParser.Parse("""[ { "url": "http://alertmanager:9093/api/v2/alerts" }, { "url": "http://other/" } ]""");

            Assert.Multiple(() =>
            {
                Assert.That(webhooks[0].Name, Is.EqualTo("webhook-1"));
                Assert.That(webhooks[1].Name, Is.EqualTo("webhook-2"));
                Assert.That(webhooks[0].Headers, Is.Empty);
                Assert.That(webhooks[0].HasTemplate, Is.False);
                Assert.That(Enum.GetValues<WebhookAlertType>().All(webhooks[0].Accepts), Is.True, "all alert types are sent by default");
            });
        }

        [Test]
        public void Template_can_be_a_string()
        {
            var webhook = WebhookSettingsParser.Parse("""[ { "Url": "https://example.com", "Template": "{ \"a\": \"#valueof($[0].labels.alertname)\" }" } ]""")[0];

            Assert.That(webhook.Template, Is.EqualTo("""{ "a": "#valueof($[0].labels.alertname)" }"""));
            Assert.That(webhook.TemplateError, Is.Null);
        }

        [TestCase("""{ "Url": "https://example.com", "Template": "not json" }""", "not valid JSON")]
        [TestCase("""{ "Url": "https://example.com", "Template": "[1, 2]" }""", "must be a JSON object")]
        [TestCase("""{ "Url": "https://example.com", "Template": { "x": "#customfunction(System.Private.CoreLib,System.IO.Path.GetTempPath)" } }""", "#customfunction")]
        [TestCase("""{ "Url": "https://example.com", "Template": { "x": "#xconcat(#,CustomFunction(a,b))" } }""", "#customfunction")]
        public void Unusable_templates_do_not_prevent_startup_but_are_reported(string webhook, string expectedError)
        {
            var parsed = WebhookSettingsParser.Parse($"[{webhook}]")[0];

            Assert.That(parsed.TemplateError, Does.Contain(expectedError));
        }

        [TestCase("{", "not valid JSON")]
        [TestCase("""{ "Url": "https://example.com" }""", "must be a JSON array")]
        [TestCase("""[ "https://example.com" ]""", "must be a JSON object")]
        [TestCase("""[ { "Name": "a" } ]""", "absolute http or https URL")]
        [TestCase("""[ { "Url": "/relative" } ]""", "absolute http or https URL")]
        [TestCase("""[ { "Url": "ftp://example.com" } ]""", "absolute http or https URL")]
        [TestCase("""[ { "Url": "https://example.com", "Headers": [ "x" ] } ]""", "'Headers' must be a JSON object")]
        [TestCase("""[ { "Url": "https://example.com", "Headers": { "X-Api-Key": 42 } } ]""", "must have a string value")]
        [TestCase("""[ { "Url": "https://example.com", "Headers": { "X-Api-Key": "a\r\nX-Injected: b" } } ]""", "must not contain line breaks")]
        [TestCase("""[ { "Url": "https://example.com", "Headers": { "Bad Header": "x" } } ]""", "invalid header name 'Bad Header'")]
        [TestCase("""[ { "Url": "https://example.com", "Template": 42 } ]""", "'Template' must be a JSON object or a string")]
        [TestCase("""[ { "Url": "https://example.com", "Alerts": [ "MessageFailed", "Nope" ] } ]""", "unsupported value \"Nope\"")]
        [TestCase("""[ { "Url": "https://example.com", "Alerts": [ "1" ] } ]""", "unsupported value \"1\"")]
        [TestCase("""[ { "Url": "https://example.com", "Alerts": [] } ]""", "non-empty array")]
        [TestCase("""[ { "Url": "https://example.com", "Name": "" } ]""", "empty Name")]
        [TestCase("""[ { "Url": "https://example.com", "Name": "a" }, { "Url": "https://example.com", "Name": "A" } ]""", "must be unique")]
        [TestCase("""[ { "Url": "https://example.com", "Tempalte": {} } ]""", "unknown property 'Tempalte'")]
        public void Invalid_configuration_fails_with_a_descriptive_error(string json, string expectedError)
        {
            var exception = Assert.Throws<WebhookConfigurationException>(() => WebhookSettingsParser.Parse(json));

            Assert.That(exception.Message, Does.Contain(expectedError));
        }

        [Test]
        public void Errors_do_not_disclose_the_url()
        {
            var exception = Assert.Throws<WebhookConfigurationException>(() => WebhookSettingsParser.Parse("""[ { "Name": "slack", "Url": "hooks.slack.com/services/T000/B000/SECRET" } ]"""));

            Assert.That(exception.Message, Does.Not.Contain("SECRET"));
            Assert.That(exception.Message, Does.Contain("slack"));
        }

        [Test]
        public void Webhooks_are_excluded_from_serialized_settings()
        {
            var settings = WebhookTestData.CreateSettings("""[ { "Url": "https://hooks.example.com/SECRET-PATH", "Headers": { "Authorization": "Bearer SECRET-TOKEN" } } ]""");

            var json = JsonSerializer.Serialize(settings);

            Assert.That(json, Does.Not.Contain("SECRET"));
        }
    }
}
