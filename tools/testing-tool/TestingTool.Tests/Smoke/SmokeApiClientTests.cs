using System.Net;
using NUnit.Framework;
using TestingTool.Smoke;

namespace TestingTool.Tests.Smoke;

public class SmokeApiClientTests
{
    static (SmokeApiClient Client, FakeHttpHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new FakeHttpHandler(respond);
        return (new SmokeApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://sc.test") }), handler);
    }

    [Test]
    public void An_unexpected_status_fails_with_the_status_and_body()
    {
        var (client, _) = Create(_ => FakeHttpHandler.Json("""{"error":"nope"}""", HttpStatusCode.InternalServerError));

        var ex = Assert.ThrowsAsync<SmokeCheckFailedException>(() => client.GetEndpoints(CancellationToken.None));

        Assert.That(ex!.Message, Does.Contain("GET /api/endpoints returned 500").And.Contain("nope"));
    }

    [Test]
    public void A_transport_error_fails_the_check_instead_of_returning_an_empty_list()
    {
        var (client, _) = Create(_ => throw new HttpRequestException("connection refused"));

        var ex = Assert.ThrowsAsync<SmokeCheckFailedException>(() => client.GetGroups(CancellationToken.None));

        Assert.That(ex!.Message, Does.Contain("could not reach http://sc.test/").And.Contain("connection refused"));
    }

    [Test]
    public async Task A_missing_failed_message_is_null_until_it_is_ingested()
    {
        var (client, _) = Create(r => r.RequestUri!.AbsolutePath.EndsWith("/missing")
            ? FakeHttpHandler.Status(HttpStatusCode.NotFound)
            : FakeHttpHandler.Json("""{"status":"retryIssued","processing_attempts":[{"headers":{"NServiceBus.MessageId":"m1"}}]}"""));

        Assert.That(await client.FindFailedMessage("missing", CancellationToken.None), Is.Null);
        var found = await client.FindFailedMessage("known", CancellationToken.None);
        Assert.That(found!.Status, Is.EqualTo("retryIssued"));
        Assert.That(found.ProcessingAttempts[^1].Headers["NServiceBus.MessageId"], Is.EqualTo("m1"));
    }

    [Test]
    public async Task Edit_sends_snake_case_names_and_reports_an_ignored_edit()
    {
        var (client, handler) = Create(_ => FakeHttpHandler.Json("""{"edit_ignored":true}"""));

        var accepted = await client.EditMessage("id1", "{}", new Dictionary<string, string> { ["NServiceBus.MessageId"] = "m1" }, CancellationToken.None);

        Assert.That(accepted, Is.False);
        Assert.That(handler.Requests[0].Body, Is.EqualTo("""{"message_body":"{}","message_headers":{"NServiceBus.MessageId":"m1"}}"""));
    }

    [Test]
    public async Task Redirect_request_uses_the_lowercase_names_of_the_api()
    {
        var (client, handler) = Create(_ => FakeHttpHandler.Status(HttpStatusCode.Created));

        await client.CreateRedirect("from-queue", "to-queue", CancellationToken.None);

        Assert.That(handler.Requests[0].Body, Is.EqualTo("""{"fromphysicaladdress":"from-queue","tophysicaladdress":"to-queue","retryexisting":false}"""));
    }

    [Test]
    public async Task Search_exposes_the_instances_that_did_not_answer()
    {
        var (client, _) = Create(_ =>
        {
            var response = FakeHttpHandler.Json("[]");
            response.Headers.Add("X-Particular-Incomplete-Results", "audit1:unavailable");
            return response;
        });

        var result = await client.SearchMessages("token", CancellationToken.None);

        Assert.That(result.Incomplete, Is.EqualTo("audit1:unavailable"));
    }

    [Test]
    public async Task Saga_is_null_while_the_primary_answers_no_content()
    {
        var (client, _) = Create(_ => FakeHttpHandler.Status(HttpStatusCode.NoContent));

        Assert.That((await client.GetSaga(Guid.NewGuid(), CancellationToken.None)).Value, Is.Null);
    }

    [Test]
    public async Task Paged_lists_are_read_until_a_short_page()
    {
        var (client, handler) = Create(r =>
        {
            var page = int.Parse(System.Web.HttpUtility.ParseQueryString(r.RequestUri!.Query)["page"]!);
            var count = page == 1 ? 100 : 3;
            var items = string.Join(",", Enumerable.Range(0, count).Select(i => $$"""{"id":"{{page}}-{{i}}","custom_check_id":"c","status":"fail","reported_at":"2026-01-01T00:00:00Z"}"""));
            return FakeHttpHandler.Json($"[{items}]");
        });

        var checks = await client.GetCustomChecks("fail", CancellationToken.None);

        Assert.That(checks, Has.Count.EqualTo(103));
        Assert.That(handler.Requests.Select(r => r.PathAndQuery), Is.EqualTo(["/api/customchecks?status=fail&page=1&per_page=100", "/api/customchecks?status=fail&page=2&per_page=100"]));
    }

    [Test]
    public async Task Message_body_follows_the_relative_body_url_of_the_audit_instance()
    {
        var (client, handler) = Create(_ => FakeHttpHandler.Json("body text"));
        var message = new SmokeApiClient.AuditedMessage("row-id", "m1", "T", "successful", null, "/messages/body-1/body?instance_id=audit-1", null, null);

        await client.GetMessageBody(message, CancellationToken.None);

        Assert.That(handler.Requests[0].PathAndQuery, Is.EqualTo("/api/messages/body-1/body?instance_id=audit-1"));
    }

    [Test]
    public void Message_body_without_a_body_url_fails_clearly()
    {
        var (client, _) = Create(_ => FakeHttpHandler.Json(""));
        var message = new SmokeApiClient.AuditedMessage("row-id", "m1", "T", "successful", null, null, null, null);

        var ex = Assert.ThrowsAsync<SmokeCheckFailedException>(() => client.GetMessageBody(message, CancellationToken.None));

        Assert.That(ex!.Message, Does.Contain("no body_url"));
    }

    [TestCase("0f8fad5b-d9cb-469f-a165-70867728950e")]
    [TestCase("CustomChecks/0f8fad5b-d9cb-469f-a165-70867728950e")]
    public async Task Custom_check_delete_uses_only_the_guid(string id)
    {
        var (client, handler) = Create(_ => FakeHttpHandler.Status(HttpStatusCode.Accepted));

        await client.DeleteCustomCheck(id, CancellationToken.None);

        Assert.That(handler.Requests[0].PathAndQuery, Is.EqualTo("/api/customchecks/0f8fad5b-d9cb-469f-a165-70867728950e"));
    }

    [TestCase("retry", true)]
    [TestCase("ARCHIVE", true)]
    [TestCase("Retention-Sweep", true)]
    [TestCase("search", false)]
    [TestCase("custom-check-failures", false)]
    public void Only_jobs_that_act_on_every_failure_group_conflict_with_a_smoke_run(string job, bool conflicts) =>
        Assert.That(SmokeJobConflicts.Conflicts(job), Is.EqualTo(conflicts));
}
