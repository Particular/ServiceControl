namespace ServiceControl.AcceptanceTests.EventLogs
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.EventLog;

    [TestFixture]
    class When_the_event_log_is_polled_with_an_etag : AcceptanceTest
    {
        [Test]
        public async Task Should_answer_not_modified_only_for_the_current_etag()
        {
            string etag = null;
            HttpStatusCode currentEtagStatus = default;
            HttpStatusCode unknownEtagStatus = default;
            var unknownEtagReturnedItems = false;

            await Define<ScenarioContext>()
                .WithEndpoint<StartingEndpoint>()
                .Done(async c =>
                {
                    var first = await this.GetRaw("/api/eventlogitems/");

                    if (first.StatusCode != HttpStatusCode.OK)
                    {
                        return false;
                    }

                    var items = await first.Content.ReadFromJsonAsync<List<EventLogItem>>(SerializerOptions);

                    // Keep polling until the endpoint's startup event has landed. An empty event
                    // log has a stable ETag of its own and would make the assertions meaningless.
                    if (items is not { Count: > 0 })
                    {
                        return false;
                    }

                    var currentEtag = ReadEtag(first);

                    if (string.IsNullOrEmpty(currentEtag))
                    {
                        return false;
                    }

                    var current = await PollUntilTheLogGoesQuiet(currentEtag);

                    if (current == null)
                    {
                        return false;
                    }

                    etag = currentEtag;
                    currentEtagStatus = current.StatusCode;

                    var unknown = await Poll("\"not-an-etag-this-instance-ever-issued\"");
                    unknownEtagStatus = unknown.StatusCode;

                    var unknownBody = await unknown.Content.ReadFromJsonAsync<List<EventLogItem>>(SerializerOptions);
                    unknownEtagReturnedItems = unknownBody is { Count: > 0 };

                    return true;
                })
                .Run();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(etag, Is.Not.Null.And.Not.Empty,
                    "the endpoint must emit an ETag or nothing downstream can cache it");

                Assert.That(currentEtagStatus, Is.EqualTo(HttpStatusCode.NotModified),
                    "a client holding the current version must be told so, not handed the page again");

                Assert.That(unknownEtagStatus, Is.EqualTo(HttpStatusCode.OK),
                    "an unrecognised validator is a cache miss: this is what stops an unconditional 304 passing");

                Assert.That(unknownEtagReturnedItems, Is.True,
                    "a cache miss must carry the items, not an empty body with a 200");
            }
        }

        // Raw header: an unquoted value fails EntityTagHeaderValue parsing, and this test
        // has to observe that rather than throw on it.
        static string ReadEtag(HttpResponseMessage response) =>
            response.Headers.TryGetValues("ETag", out var values) ? values.FirstOrDefault() : null;

        // The endpoint keeps writing startup events, so a 200 carrying a *different* ETag only
        // means the log moved on between the two requests, and says nothing about conditional
        // GET support. Poll again with the ETag that response handed back, until either the log
        // stops changing (304, or a 200 repeating the validator we sent, which is the real
        // failure) or the attempts run out and the caller retries from a fresh read.
        async Task<HttpResponseMessage> PollUntilTheLogGoesQuiet(string etag)
        {
            for (var attempt = 0; attempt < MaxPollAttempts; attempt++)
            {
                var response = await Poll(etag);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return response;
                }

                var newEtag = ReadEtag(response);

                if (string.IsNullOrEmpty(newEtag) || newEtag == etag)
                {
                    return response;
                }

                etag = newEtag;
            }

            return null;
        }

        Task<HttpResponseMessage> Poll(string ifNoneMatch)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/eventlogitems/");
            request.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);

            return HttpClient.SendAsync(request);
        }

        const int MaxPollAttempts = 5;

        public class StartingEndpoint : EndpointConfigurationBuilder
        {
            public StartingEndpoint() =>
                EndpointSetup<DefaultServerWithoutAudit>(c => c.SendHeartbeatTo(Settings.DEFAULT_INSTANCE_NAME));
        }
    }
}