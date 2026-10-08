namespace ServiceControl.AcceptanceTests.Recoverability.ExternalIntegration
{
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using ServiceControl.Notifications.Webhooks;

    class When_webhooks_are_configured : ExternalIntegrationAcceptanceTest
    {
        [Test]
        public async Task Should_notify_when_a_message_fails_and_when_it_is_resolved()
        {
            var receiver = new WebhookReceiver();

            SetSettings = settings =>
            {
                settings.ServicePulseUrl = "http://servicepulse.local";
                settings.Webhooks = WebhookSettingsParser.Parse("""
                    [
                      {
                        "Name": "alertmanager",
                        "Url": "http://alertmanager.local/api/v2/alerts",
                        "Headers": { "Authorization": "Bearer alertmanager-token" }
                      },
                      {
                        "Name": "pagerduty",
                        "Url": "http://pagerduty.local/v2/enqueue",
                        "Alerts": [ "MessageFailed" ],
                        "Template": {
                          "routing_key": "R0UT1NGK3Y",
                          "event_action": "#ifcondition(#exists($[0].endsAt),true,resolve,trigger)",
                          "dedup_key": "#valueof($[0].annotations.dedup_key)",
                          "payload": {
                            "summary": "#valueof($[0].annotations.summary)",
                            "source": "#valueof($[0].labels.endpoint)",
                            "severity": "#valueof($[0].labels.severity)"
                          }
                        }
                      }
                    ]
                    """);
            };
            CustomizeHostBuilder = builder => builder.Services
                .AddHttpClient(WebhookSender.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => receiver);

            var context = await Define<Context>()
                .WithEndpoint<ErrorSender>(b => b.When(session => Task.CompletedTask).DoNotFailOnErrorMessages())
                .Do("WaitUntilErrorsContainsFailedMessage",
                    async ctx => await this.TryGet<FailedMessage>($"/api/errors/{ctx.FailedMessageId}") != null)
                .Do("WaitForFiringAlerts", ctx => Task.FromResult(receiver.HasReceived(ctx, resolved: false)))
                .Do("Retry", async ctx => await this.Post<object>($"/api/errors/{ctx.FailedMessageId}/retry"))
                .Do("WaitForResolvedAlerts", ctx => Task.FromResult(receiver.HasReceived(ctx, resolved: true)))
                .Done(ctx => receiver.HasReceived(ctx, resolved: true))
                .Run();

            var failedMessageId = context.FailedMessageId.ToString();

            var alertmanagerRequests = receiver.Requests.Where(r => r.Url.Host == "alertmanager.local").ToArray();
            var firing = alertmanagerRequests.SelectMany(r => r.Body.AsArray()).First(a => a["endsAt"] == null);
            var resolved = alertmanagerRequests.SelectMany(r => r.Body.AsArray()).First(a => a["endsAt"] != null);

            Assert.Multiple(() =>
            {
                Assert.That(alertmanagerRequests.All(r => r.Authorization == "Bearer alertmanager-token"), Is.True);
                Assert.That(alertmanagerRequests.All(r => r.ContentType == "application/json"), Is.True);

                Assert.That(firing["labels"]["alertname"].GetValue<string>(), Is.EqualTo("MessageFailed"));
                Assert.That(firing["labels"]["failed_message_id"].GetValue<string>(), Is.EqualTo(failedMessageId));
                Assert.That(firing["labels"]["endpoint"].GetValue<string>(), Is.EqualTo(nameof(ErrorSender)));
                Assert.That(firing["annotations"]["exception_type"].GetValue<string>(), Is.EqualTo("System.Exception"));
                Assert.That(firing["annotations"]["exception_message"].GetValue<string>(), Is.EqualTo("An error occurred"));
                Assert.That(firing["startsAt"], Is.Not.Null);
                Assert.That(firing["generatorURL"].GetValue<string>(), Is.EqualTo($"http://servicepulse.local/#/failed-messages/message/{failedMessageId}"));

                Assert.That(resolved["labels"].ToJsonString(), Is.EqualTo(firing["labels"].ToJsonString()), "the resolved alert must have the same labels as the firing alert");
                Assert.That(resolved["annotations"]["dedup_key"].GetValue<string>(), Is.EqualTo(firing["annotations"]["dedup_key"].GetValue<string>()));
            });

            var pagerDutyRequests = receiver.Requests.Where(r => r.Url.Host == "pagerduty.local").Select(r => r.Body).ToArray();
            var trigger = pagerDutyRequests.First(b => b["event_action"].GetValue<string>() == "trigger");
            var resolve = pagerDutyRequests.First(b => b["event_action"].GetValue<string>() == "resolve");

            Assert.Multiple(() =>
            {
                Assert.That(trigger["routing_key"].GetValue<string>(), Is.EqualTo("R0UT1NGK3Y"));
                Assert.That(trigger["dedup_key"].GetValue<string>(), Is.EqualTo(firing["annotations"]["dedup_key"].GetValue<string>()));
                Assert.That(trigger["payload"]["source"].GetValue<string>(), Is.EqualTo(nameof(ErrorSender)));
                Assert.That(trigger["payload"]["severity"].GetValue<string>(), Is.EqualTo("error"));
                Assert.That(resolve["dedup_key"].GetValue<string>(), Is.EqualTo(trigger["dedup_key"].GetValue<string>()));
            });
        }

        record ReceivedRequest(System.Uri Url, string Authorization, string ContentType, JsonNode Body);

        class WebhookReceiver : HttpMessageHandler
        {
            public ConcurrentQueue<ReceivedRequest> Requests { get; } = new();

            public bool HasReceived(Context context, bool resolved)
            {
                var failedMessageId = context.FailedMessageId.ToString();

                var alertmanager = Requests
                    .Where(r => r.Url.Host == "alertmanager.local")
                    .SelectMany(r => r.Body.AsArray())
                    .Any(a => a["labels"]?["failed_message_id"]?.GetValue<string>() == failedMessageId && a["endsAt"] != null == resolved);

                var pagerDuty = Requests
                    .Where(r => r.Url.Host == "pagerduty.local")
                    .Any(r => r.Body["event_action"]?.GetValue<string>() == (resolved ? "resolve" : "trigger"));

                return alertmanager && pagerDuty;
            }

#pragma warning disable PS0003 // HttpMessageHandler.SendAsync override signature is fixed
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
#pragma warning restore PS0003
            {
                var body = await request.Content.ReadAsStringAsync(cancellationToken);
                Requests.Enqueue(new ReceivedRequest(
                    request.RequestUri,
                    request.Headers.Authorization?.ToString(),
                    request.Content.Headers.ContentType?.MediaType,
                    JsonNode.Parse(body)));

                return new HttpResponseMessage(HttpStatusCode.Accepted);
            }
        }
    }
}
