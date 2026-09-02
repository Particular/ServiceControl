namespace ServiceControl.AcceptanceTests.WebApi;

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AcceptanceTesting;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;
using ServiceControl.Api.Contracts;

class When_triggering_a_manual_retention_sweep : AcceptanceTest
{
    [Test]
    public async Task Should_be_available_on_efcore_persisters()
    {
        if (StorageConfiguration.PersistenceType == "RavenDB")
        {
            Assert.Ignore("RavenDB has no sweeper — covered by Should_return_501_on_a_ravendb_backed_instance.");
            return;
        }

        HttpStatusCode started = default;
        HttpStatusCode invalidCutoff = default;

        await Define<Context>()
            .Done(async _ =>
            {
                // Trigger a sweep with a past UTC cutoff. The delete work runs in the background,
                // so the call returns 202 Accepted immediately.
                using var response = await HttpClient.PostAsJsonAsync(
                    "/api/retention/sweep",
                    new RetentionSweepRequest { ErrorCutoff = DateTime.UtcNow.AddDays(-30) },
                    SerializerOptions);

                started = response.StatusCode;

                // A future-dated cutoff is rejected with 400.
                using var badRequest = await HttpClient.PostAsJsonAsync(
                    "/api/retention/sweep",
                    new RetentionSweepRequest { ErrorCutoff = DateTime.UtcNow.AddDays(1) },
                    SerializerOptions);

                invalidCutoff = badRequest.StatusCode;

                return true;
            })
            .Run();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(started, Is.EqualTo(HttpStatusCode.Accepted), "the sweep should start in the background");
            Assert.That(invalidCutoff, Is.EqualTo(HttpStatusCode.BadRequest), "a future cutoff must be rejected");
        }

        // The status endpoint must report the run, and the background sweep must complete.
        var status = await WaitUntilSweepFinishes();
        Assert.That(status.IsRunning, Is.False, "the background sweep must complete");
        Assert.That(status.LastStartedAt, Is.Not.Null);
    }

    [Test]
    public async Task Should_report_background_completion_via_status()
    {
        if (StorageConfiguration.PersistenceType == "RavenDB")
        {
            Assert.Ignore("RavenDB has no sweeper — covered by Should_return_501_on_a_ravendb_backed_instance.");
            return;
        }

        await Define<Context>()
            .Done(async _ =>
            {
                using var response = await HttpClient.PostAsJsonAsync(
                    "/api/retention/sweep",
                    new RetentionSweepRequest { ErrorCutoff = DateTime.UtcNow.AddDays(-30) },
                    SerializerOptions);

                return response.StatusCode == HttpStatusCode.Accepted;
            })
            .Run();

        var status = await WaitUntilSweepFinishes();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(status.IsRunning, Is.False);
            Assert.That(status.LastFinishedAt, Is.Not.Null, "a completed run records its finish time");
        }
    }

    [Test]
    public async Task Should_return_501_on_a_ravendb_backed_instance()
    {
        if (StorageConfiguration.PersistenceType != "RavenDB")
        {
            Assert.Ignore("EFCore persisters support the sweep — covered by the efcore tests.");
            return;
        }

        HttpStatusCode postStatus = default;
        HttpStatusCode getStatus = default;

        await Define<Context>()
            .Done(async _ =>
            {
                using var response = await HttpClient.PostAsJsonAsync(
                    "/api/retention/sweep",
                    new RetentionSweepRequest { ErrorCutoff = DateTime.UtcNow.AddDays(-30) },
                    SerializerOptions);

                postStatus = response.StatusCode;

                using var status = await HttpClient.GetAsync("/api/retention/sweep/status");

                getStatus = status.StatusCode;

                return true;
            })
            .Run();

        using (Assert.EnterMultipleScope())
        {
            // RavenDB retention is the server-side @expires bundle; there is no cutoff-based sweeper
            // to trigger, so the optional IRetentionSweeper resolution is absent and both verbs
            // return 501 Not Implemented.
            Assert.That(postStatus, Is.EqualTo(HttpStatusCode.NotImplemented), "POST must report not-supported on RavenDB");
            Assert.That(getStatus, Is.EqualTo(HttpStatusCode.NotImplemented), "GET status must report not-supported on RavenDB");
        }
    }

    async Task<RetentionSweepStatus> WaitUntilSweepFinishes(TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));

        while (DateTime.UtcNow < deadline)
        {
            using var response = await HttpClient.GetAsync("/api/retention/sweep/status");

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var status = await response.Content.ReadFromJsonAsync<RetentionSweepStatus>(SerializerOptions);

                if (status is { IsRunning: false })
                {
                    return status;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }

        throw new Exception("The manual retention sweep did not finish within the timeout.");
    }

    class Context : ScenarioContext;
}