using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NServiceBus;
using NUnit.Framework;
using ServiceControl.Plugin.CustomChecks.Messages;
using TestingTool.Smoke;

namespace TestingTool.Tests.Smoke;

public class SmokeChecksTests
{
    const string RunId = "smoke-test0001";

    FakeMessageSession session = null!;
    SmokeObservations observations = null!;

    [SetUp]
    public void SetUp()
    {
        session = new FakeMessageSession();
        observations = new SmokeObservations();
    }

    SmokeChecks CreateChecks(Func<HttpRequestMessage, HttpResponseMessage> serviceControl) =>
        new(new SmokeApiClient(new HttpClient(new FakeHttpHandler(serviceControl)) { BaseAddress = new Uri("http://sc.test") }),
            new SmokeMonitoringClient(new HttpClient(new FakeHttpHandler(_ => FakeHttpHandler.Json("[]"))) { BaseAddress = new Uri("http://monitoring.test") }),
            session,
            observations,
            Options.Create(new TestingToolOptions()),
            new ConfigurationBuilder().Build());

    static SmokeContext Context() => new(RunId, TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(100));

    static async Task Run(SmokeChecks checks, string id, SmokeContext context)
    {
        try
        {
            await checks.Create().Single(c => c.Id == id).Run!(context, CancellationToken.None);
        }
        finally
        {
            foreach (var cleanup in context.Cleanups.Reverse())
            {
                await cleanup(CancellationToken.None);
            }
        }
    }

    IEnumerable<ReportCustomCheckResult> Reports => session.Sent.Select(s => s.Message).OfType<ReportCustomCheckResult>();

    [Test]
    public async Task Custom_check_passes_when_the_api_shows_this_runs_check_fail_then_pass()
    {
        var checks = CreateChecks(r =>
        {
            var failed = r.RequestUri!.Query.Contains("status=fail");
            var own = Reports.LastOrDefault();
            var ownIsListed = own is not null && own.HasFailed == failed;
            var entry = ownIsListed ? $$"""{"id":"u1","custom_check_id":"{{own!.CustomCheckId}}","status":"{{(failed ? "fail" : "pass")}}","failure_reason":{{JsonSerializer.Serialize(own.FailureReason)}},"reported_at":"2026-01-01T00:00:00Z"}""" : "";
            return r.Method == HttpMethod.Delete ? FakeHttpHandler.Status(HttpStatusCode.Accepted) : FakeHttpHandler.Json($"[{entry}]");
        });

        await Run(checks, "custom-check-fail-then-pass", Context());

        Assert.That(Reports.Select(r => r.HasFailed).ToArray(), Is.EqualTo(new[] { true, false }));
        Assert.That(Reports.Select(r => r.CustomCheckId).Distinct().Single(), Is.EqualTo($"Smoke.{RunId}"));
    }

    [Test]
    public void Custom_check_ignores_a_failed_check_from_another_run()
    {
        var checks = CreateChecks(r => FakeHttpHandler.Json("""[{"id":"u0","custom_check_id":"Smoke.smoke-older","status":"fail","failure_reason":"x","reported_at":"2026-01-01T00:00:00Z"}]"""));

        var ex = Assert.ThrowsAsync<SmokeCheckFailedException>(() => Run(checks, "custom-check-fail-then-pass", Context()));

        Assert.That(ex!.Message, Does.Contain($"no failed check with id Smoke.{RunId}"));
    }

    [Test]
    public void Audit_check_fails_without_a_remote_audit_instance()
    {
        var checks = CreateChecks(_ => FakeHttpHandler.Json("[]"));

        var ex = Assert.ThrowsAsync<SmokeCheckFailedException>(() => Run(checks, "audit-ingestion-fulltext", Context()));

        Assert.That(ex!.Message, Does.Contain("no remote audit instance"));
    }

    [Test]
    public void Audit_check_ignores_search_hits_for_other_messages()
    {
        var checks = CreateChecks(r => r.RequestUri!.AbsolutePath.EndsWith("/remotes")
            ? FakeHttpHandler.Json("""[{"api_uri":"http://audit","status":"online"}]""")
            : FakeHttpHandler.Json("""[{"id":"a1","message_id":"someone-elses","message_type":"X","status":"successful"}]"""));

        var ex = Assert.ThrowsAsync<SmokeCheckFailedException>(() => Run(checks, "audit-ingestion-fulltext", Context()));

        Assert.That(ex!.Message, Does.Contain("none with message id"));
    }

    [Test]
    public async Task Retry_single_retries_only_the_chosen_message_in_a_group_unique_to_the_run()
    {
        var server = new FailureServer(session, observations);
        var checks = CreateChecks(server.Handle);

        await Run(checks, "retry-single", Context());

        var failing = session.Sent.Select(s => (Message: (SmokeMessage)s.Message, Headers: s.Options.GetHeaders())).ToList();
        Assert.That(failing, Has.Count.EqualTo(2));
        Assert.That(failing.Select(f => f.Message.FailureType).Distinct().Single(), Is.EqualTo($"TestingTool.Smoke.Failure_{RunId.Replace('-', '_')}_retry_single"));
        Assert.That(failing.All(f => f.Message.Fail && f.Headers[SmokeHeaders.RunId] == RunId), Is.True);
        Assert.That(server.Retried, Has.Count.EqualTo(1));
    }

    [Test]
    public void Retry_single_fails_when_the_retry_also_resolves_the_sibling()
    {
        var server = new FailureServer(session, observations) { RetryResolvesEverything = true };
        var checks = CreateChecks(server.Handle);

        var ex = Assert.ThrowsAsync<SmokeCheckFailedException>(() => Run(checks, "retry-single", Context()));

        Assert.That(ex!.Message, Does.Contain("also acted on its sibling"));
    }

    [Test]
    public void Retry_group_fails_when_a_message_is_not_handled_even_though_the_api_accepted_the_retry()
    {
        var server = new FailureServer(session, observations) { HandlesRetries = false };
        var checks = CreateChecks(server.Handle);

        var ex = Assert.ThrowsAsync<SmokeCheckFailedException>(() => Run(checks, "retry-group", Context()));

        Assert.That(ex!.Message, Does.Contain("handled by the endpoint: False"));
    }

    [Test]
    public async Task Failed_messages_left_unresolved_are_archived_when_the_check_ends()
    {
        var server = new FailureServer(session, observations);
        var checks = CreateChecks(server.Handle);

        await Run(checks, "retry-single", Context());

        Assert.That(server.Archived, Has.Count.EqualTo(1));
        Assert.That(server.Archived.Single(), Is.Not.EqualTo(server.Retried.Single()));
    }

    [Test]
    public async Task Endpoint_record_cleanup_deletes_only_the_named_run_endpoints()
    {
        var deleted = new List<string>();
        var checks = CreateChecks(r =>
        {
            if (r.Method == HttpMethod.Delete)
            {
                deleted.Add(r.RequestUri!.AbsolutePath);
                return FakeHttpHandler.Status(HttpStatusCode.NoContent);
            }

            return FakeHttpHandler.Json("""
                [{"id":"00000000-0000-0000-0000-000000000001","name":"TestingTool.Smoke.smoke-test0001.Source","monitor_heartbeat":false},
                 {"id":"00000000-0000-0000-0000-000000000002","name":"TestingTool.Smoke.smoke-test0001.Target","monitor_heartbeat":false},
                 {"id":"00000000-0000-0000-0000-000000000003","name":"TestingTool.Smoke.smoke-other.Source","monitor_heartbeat":false},
                 {"id":"00000000-0000-0000-0000-000000000004","name":"Sales","monitor_heartbeat":true}]
                """);
        });
        var context = Context();

        checks.DeleteEndpointRecords(context, $"TestingTool.Smoke.{RunId}.Source", $"TestingTool.Smoke.{RunId}.Target");
        foreach (var cleanup in context.Cleanups)
        {
            await cleanup(CancellationToken.None);
        }

        Assert.That(deleted, Is.EqualTo(["/api/endpoints/00000000-0000-0000-0000-000000000001", "/api/endpoints/00000000-0000-0000-0000-000000000002"]));
    }

    /// <summary>Answers the failed-message API calls for the messages the checks sent through the fake session.</summary>
    sealed class FailureServer(FakeMessageSession session, SmokeObservations observations)
    {
        readonly Dictionary<string, string> status = [];

        public bool RetryResolvesEverything { get; init; }
        public bool HandlesRetries { get; init; } = true;
        public List<string> Retried { get; } = [];
        public List<string> Archived { get; } = [];

        IEnumerable<(string MessageId, SmokeMessage Message, string ExceptionType)> Failing => session.Sent
            .Where(s => s.Message is SmokeMessage)
            .Select(s => (s.Options.GetMessageId()!, (SmokeMessage)s.Message, ((SmokeMessage)s.Message).FailureType));

        string StatusOf(string messageId) => status.GetValueOrDefault(messageId, "unresolved");

        public HttpResponseMessage Handle(HttpRequestMessage request)
        {
            var path = request.RequestUri!.AbsolutePath;
            var failing = Failing.ToList();
            if (path == "/api/recoverability/groups")
            {
                return FakeHttpHandler.Json($$"""[{"id":"g1","title":"{{failing[0].ExceptionType}}: SmokeMessageHandler.Fail()","count":{{failing.Count}}}]""");
            }

            if (path == "/api/recoverability/groups/g1/errors")
            {
                return FakeHttpHandler.Json("[" + string.Join(",", failing.Select(f => $$"""{"id":"{{f.MessageId}}","message_id":"{{f.MessageId}}","status":"{{StatusOf(f.MessageId)}}","queue_address":"TestingTool.Load"}""")) + "]");
            }

            if (path == "/api/recoverability/groups/g1/errors/retry")
            {
                failing.ForEach(f => Resolve(f.MessageId, f.Message));
                return FakeHttpHandler.Status(HttpStatusCode.Accepted);
            }

            if (path == "/api/errors/archive")
            {
                var ids = JsonSerializer.Deserialize<string[]>(request.Content!.ReadAsStringAsync().Result)!;
                Archived.AddRange(ids);
                return FakeHttpHandler.Status(HttpStatusCode.Accepted);
            }

            var id = path.Split('/')[3];
            if (path.EndsWith("/retry"))
            {
                Retried.Add(id);
                failing.Where(f => RetryResolvesEverything || f.MessageId == id).ToList().ForEach(f => Resolve(f.MessageId, f.Message));
                return FakeHttpHandler.Status(HttpStatusCode.Accepted);
            }

            return FakeHttpHandler.Json($$$"""{"status":"{{{StatusOf(id)}}}","processing_attempts":[{"headers":{}}]}""");
        }

        void Resolve(string messageId, SmokeMessage message)
        {
            if (HandlesRetries)
            {
                observations.Release(message.Marker);
                SmokeMessageHandler.Process(observations, message, null, "Endpoint");
            }

            status[messageId] = "resolved";
        }
    }
}
