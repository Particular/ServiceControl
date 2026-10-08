namespace ServiceControl.UnitTests.Notifications.Webhooks
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Polly;
    using Polly.Timeout;
    using ServiceControl.Notifications.Webhooks;
    using ServiceControl.UnitTests.Operations;

    [TestFixture]
    public class WebhookSenderTests
    {
        [SetUp]
        public void SetUp()
        {
            handler = new RecordingHttpHandler();
            domainEvents = new FakeDomainEvents();
        }

        [TearDown]
        public void TearDown() => handler.Dispose();

        WebhookSender CreateSender(string webhooksJson = DefaultWebhook, WebhookDeliveryOptions options = null) =>
            WebhookSenderFactory.Create(WebhookTestData.CreateSettings(webhooksJson), handler, domainEvents, options);

        static AlertmanagerAlert Alert(string endpoint = "Sales", DateTime? endsAt = null) => new()
        {
            Labels = new() { ["alertname"] = "HeartbeatStopped", ["endpoint"] = endpoint },
            Annotations = new() { ["summary"] = $"{endpoint} stopped", ["dedup_key"] = $"key-{endpoint}" },
            StartsAt = WebhookTestData.TimeOfFailure,
            EndsAt = endsAt
        };

        WebhookNotificationFailed Failure => domainEvents.RaisedEvents.OfType<WebhookNotificationFailed>().SingleOrDefault();

        [Test]
        public async Task Posts_the_alertmanager_payload_as_json()
        {
            await CreateSender().Send("alertmanager", [Alert("Sales"), Alert("Billing", WebhookTestData.Now)]);

            var request = handler.Requests.Single();
            Assert.Multiple(() =>
            {
                Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
                Assert.That(request.Uri, Is.EqualTo(new Uri("http://alertmanager:9093/api/v2/alerts")));
                Assert.That(request.ContentType, Is.EqualTo("application/json"));
                Assert.That(request.Headers["Content-Type"], Does.Contain("charset=utf-8"));
                Assert.That(request.Headers["User-Agent"], Does.StartWith("ServiceControl/"));
                Assert.That(request.Body, Is.EqualTo(AlertmanagerPayload.Serialize([Alert("Sales"), Alert("Billing", WebhookTestData.Now)])));
                Assert.That(Failure, Is.Null);
            });
        }

        [Test]
        public async Task Configured_headers_are_sent()
        {
            var sender = CreateSender("""
                [ {
                    "Name": "alertmanager",
                    "Url": "https://example.com/hook",
                    "Headers": { "Authorization": "Bearer abc", "X-Api-Key": "key", "User-Agent": "custom-agent", "Content-Type": "application/vnd.custom+json" }
                } ]
                """);

            await sender.Send("alertmanager", [Alert()]);

            var request = handler.Requests.Single();
            Assert.Multiple(() =>
            {
                Assert.That(request.Headers["Authorization"], Is.EqualTo("Bearer abc"));
                Assert.That(request.Headers["X-Api-Key"], Is.EqualTo("key"));
                Assert.That(request.Headers["User-Agent"], Is.EqualTo("custom-agent"));
                Assert.That(request.Headers["Content-Type"], Is.EqualTo("application/vnd.custom+json"));
            });
        }

        [Test]
        public async Task Template_is_applied_before_sending()
        {
            var sender = CreateSender("""
                [ {
                    "Name": "alertmanager",
                    "Url": "https://example.com/hook",
                    "Template": { "action": "#ifcondition(#exists($[0].endsAt),true,resolve,trigger)", "key": "#valueof($[0].annotations.dedup_key)" }
                } ]
                """);

            await sender.Send("alertmanager", [Alert(endsAt: WebhookTestData.Now)]);

            Assert.That(handler.Requests.Single().Body, Is.EqualTo("""{"action":"resolve","key":"key-Sales"}"""));
        }

        [Test]
        public async Task Invalid_template_is_reported_without_sending()
        {
            var sender = CreateSender("""[ { "Name": "alertmanager", "Url": "https://example.com/hook", "Template": "{ broken" } ]""");

            await sender.Send("alertmanager", [Alert()]);

            Assert.That(handler.Requests, Is.Empty);
            Assert.That(Failure.Reason, Does.Contain("template").And.Contain("not valid JSON"));
            Assert.That(Failure.AlertCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Template_failing_at_runtime_is_reported_without_sending()
        {
            var sender = CreateSender("""[ { "Name": "alertmanager", "Url": "https://example.com/hook", "Template": { "x": "#ifcondition(1)" } } ]""");

            await sender.Send("alertmanager", [Alert()]);

            Assert.That(handler.Requests, Is.Empty);
            Assert.That(Failure.Reason, Does.Contain("The template could not be applied"));
        }

        [Test]
        public async Task Notifications_for_webhooks_that_are_no_longer_configured_are_dropped()
        {
            await CreateSender().Send("removed", [Alert()]);

            Assert.That(handler.Requests, Is.Empty);
            Assert.That(Failure, Is.Null);
        }

        [Test]
        public async Task Webhook_names_are_case_insensitive()
        {
            await CreateSender().Send("ALERTMANAGER", [Alert()]);

            Assert.That(handler.Requests, Has.Count.EqualTo(1));
        }

        [TestCase(HttpStatusCode.InternalServerError)]
        [TestCase(HttpStatusCode.BadGateway)]
        [TestCase(HttpStatusCode.ServiceUnavailable)]
        [TestCase(HttpStatusCode.TooManyRequests)]
        [TestCase(HttpStatusCode.RequestTimeout)]
        public async Task Transient_failures_are_retried(HttpStatusCode statusCode)
        {
            handler.RespondWith(statusCode, statusCode, HttpStatusCode.Accepted);

            await CreateSender().Send("alertmanager", [Alert()]);

            Assert.That(handler.Requests, Has.Count.EqualTo(3));
            Assert.That(handler.Requests.Select(r => r.Body).Distinct().Count(), Is.EqualTo(1), "every attempt sends the same payload");
            Assert.That(Failure, Is.Null);
        }

        [Test]
        public async Task Connection_failures_are_retried()
        {
            handler.RespondWith((_, _) => throw new HttpRequestException("Connection refused"), (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

            await CreateSender().Send("alertmanager", [Alert()]);

            Assert.That(handler.Requests, Has.Count.EqualTo(2));
            Assert.That(Failure, Is.Null);
        }

        [Test]
        public async Task Slow_responses_time_out_and_are_retried()
        {
            handler.RespondWith(
                async (_, token) =>
                {
                    await Task.Delay(Timeout.Infinite, token);
                    return null;
                },
                (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

            await CreateSender(options: new WebhookDeliveryOptions
            {
                MaxRetryAttempts = 1,
                RetryDelay = TimeSpan.FromMilliseconds(1),
                AttemptTimeout = TimeSpan.FromMilliseconds(50)
            }).Send("alertmanager", [Alert()]);

            Assert.That(handler.Requests, Has.Count.EqualTo(2));
            Assert.That(Failure, Is.Null);
        }

        [Test]
        public async Task Timeouts_are_reported_once_retries_are_exhausted()
        {
            handler.RespondWith(async (_, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return null;
            });

            await CreateSender(options: new WebhookDeliveryOptions
            {
                MaxRetryAttempts = 1,
                RetryDelay = TimeSpan.FromMilliseconds(1),
                AttemptTimeout = TimeSpan.FromMilliseconds(50)
            }).Send("alertmanager", [Alert()]);

            Assert.That(handler.Requests, Has.Count.EqualTo(2));
            Assert.That(Failure.Reason, Is.EqualTo("The webhook did not respond in time."));
        }

        [TestCase(HttpStatusCode.BadRequest)]
        [TestCase(HttpStatusCode.Unauthorized)]
        [TestCase(HttpStatusCode.NotFound)]
        public async Task Permanent_failures_are_reported_without_retrying(HttpStatusCode statusCode)
        {
            handler.RespondWith((_, _) => Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent("""{"error":"invalid routing key"}""") }));

            await CreateSender().Send("alertmanager", [Alert()]);

            Assert.That(handler.Requests, Has.Count.EqualTo(1));
            Assert.That(Failure.WebhookName, Is.EqualTo("alertmanager"));
            Assert.That(Failure.Reason, Does.Contain($"HTTP {(int)statusCode}").And.Contain("invalid routing key"));
        }

        [Test]
        public async Task Failures_are_reported_once_retries_are_exhausted()
        {
            handler.RespondWith(HttpStatusCode.ServiceUnavailable);

            await CreateSender().Send("alertmanager", [Alert(), Alert("Billing")]);

            Assert.That(handler.Requests, Has.Count.EqualTo(1 + WebhookSenderFactory.FastRetries.MaxRetryAttempts));
            Assert.That(Failure.Reason, Does.Contain("HTTP 503"));
            Assert.That(Failure.AlertCount, Is.EqualTo(2));
        }

        [Test]
        public async Task Long_error_responses_are_truncated()
        {
            handler.RespondWith((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(new string('x', 10_000)) }));

            await CreateSender().Send("alertmanager", [Alert()]);

            Assert.That(Failure.Reason.Length, Is.LessThan(600));
        }

        [Test]
        public async Task Exceptions_that_are_not_transient_are_reported_without_retrying()
        {
            handler.RespondWith((_, _) => throw new InvalidOperationException("unexpected"));

            await CreateSender().Send("alertmanager", [Alert()]);

            Assert.That(handler.Requests, Has.Count.EqualTo(1));
            Assert.That(Failure.Reason, Does.Contain("unexpected"));
        }

        [Test]
        public void Cancellation_is_not_reported_as_a_failure()
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            handler.RespondWith(async (_, token) =>
            {
                await cancellationTokenSource.CancelAsync();
                await Task.Delay(Timeout.Infinite, token);
                return null;
            });

            Assert.CatchAsync<OperationCanceledException>(() => CreateSender().Send("alertmanager", [Alert()], cancellationTokenSource.Token));
            Assert.That(Failure, Is.Null);
        }

        [Test]
        public async Task A_failure_to_record_the_failure_does_not_throw()
        {
            handler.RespondWith(HttpStatusCode.BadRequest);
            var sender = WebhookSenderFactory.Create(WebhookTestData.CreateSettings(DefaultWebhook), handler, new ThrowingDomainEvents());

            await sender.Send("alertmanager", [Alert()]);

            Assert.That(handler.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public void Retry_after_delta_is_respected_up_to_the_maximum_delay()
        {
            var now = DateTimeOffset.UtcNow;
            using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);

            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(3));
            Assert.That(WebhookSender.GetRetryAfter(response, now, TimeSpan.FromSeconds(10)), Is.EqualTo(TimeSpan.FromSeconds(3)));

            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(5));
            Assert.That(WebhookSender.GetRetryAfter(response, now, TimeSpan.FromSeconds(10)), Is.EqualTo(TimeSpan.FromSeconds(10)));
        }

        [Test]
        public void Retry_after_date_is_respected()
        {
            var now = new DateTimeOffset(2025, 3, 4, 10, 0, 0, TimeSpan.Zero);
            using var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

            response.Headers.RetryAfter = new RetryConditionHeaderValue(now.AddSeconds(4));
            Assert.That(WebhookSender.GetRetryAfter(response, now, TimeSpan.FromSeconds(10)), Is.EqualTo(TimeSpan.FromSeconds(4)));

            response.Headers.RetryAfter = new RetryConditionHeaderValue(now.AddSeconds(-4));
            Assert.That(WebhookSender.GetRetryAfter(response, now, TimeSpan.FromSeconds(10)), Is.Null, "dates in the past fall back to the backoff delay");
        }

        [Test]
        public void Without_retry_after_the_backoff_delay_is_used()
        {
            using var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

            Assert.That(WebhookSender.GetRetryAfter(response, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(10)), Is.Null);
            Assert.That(WebhookSender.GetRetryAfter(null, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(10)), Is.Null);
        }

        [TestCase(200, false)]
        [TestCase(202, false)]
        [TestCase(400, false)]
        [TestCase(401, false)]
        [TestCase(404, false)]
        [TestCase(408, true)]
        [TestCase(429, true)]
        [TestCase(500, true)]
        [TestCase(503, true)]
        public void Status_codes_are_classified(int statusCode, bool transient)
        {
            using var response = new HttpResponseMessage((HttpStatusCode)statusCode);

            Assert.That(WebhookSender.IsTransient(Outcome.FromResult(response)), Is.EqualTo(transient));
        }

        [Test]
        public void Exceptions_are_classified()
        {
            Assert.Multiple(() =>
            {
                Assert.That(WebhookSender.IsTransient(Outcome.FromException<HttpResponseMessage>(new HttpRequestException())), Is.True);
                Assert.That(WebhookSender.IsTransient(Outcome.FromException<HttpResponseMessage>(new TimeoutRejectedException())), Is.True);
                Assert.That(WebhookSender.IsTransient(Outcome.FromException<HttpResponseMessage>(new InvalidOperationException())), Is.False);
            });
        }

        [Test]
        public void Default_payload_is_used_without_template()
        {
            var webhook = WebhookSettingsParser.Parse(DefaultWebhook)[0];

            var payload = JsonNode.Parse(WebhookSender.BuildPayload(webhook, [Alert()]));

            Assert.That(payload.AsArray().Single()["labels"]["alertname"].GetValue<string>(), Is.EqualTo("HeartbeatStopped"));
        }

        class ThrowingDomainEvents : ServiceControl.Infrastructure.DomainEvents.IDomainEvents
        {
            public Task Raise<T>(T domainEvent, CancellationToken cancellationToken = default) where T : ServiceControl.Infrastructure.DomainEvents.IDomainEvent =>
                throw new InvalidOperationException("event log unavailable");
        }

        const string DefaultWebhook = """[ { "Name": "alertmanager", "Url": "http://alertmanager:9093/api/v2/alerts" } ]""";

        RecordingHttpHandler handler;
        FakeDomainEvents domainEvents;
    }
}
