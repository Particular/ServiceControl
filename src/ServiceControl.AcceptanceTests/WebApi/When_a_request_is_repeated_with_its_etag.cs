namespace ServiceControl.AcceptanceTests.WebApi
{
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    class When_a_request_is_repeated_with_its_etag : AcceptanceTest
    {
        [TestCase("/api/customchecks", "GET")]
        [TestCase("/api/eventlogitems", "GET")]
        [TestCase("/api/redirects", "GET")]
        [TestCase("/api/redirect", "HEAD")]
        [TestCase("/api/errors/queues/addresses", "GET")]
        [TestCase("/api/errors", "GET")]
        [TestCase("/api/errors", "HEAD")]
        [TestCase("/api/messages", "GET")]
        [TestCase("/api/messages/search?q=anything", "GET")]
        [TestCase("/api/messages/search/anything", "GET")]
        [TestCase("/api/messages2?page_size=10", "GET")]
        [TestCase("/api/endpoints/no-such-endpoint/messages", "GET")]
        [TestCase("/api/endpoints/no-such-endpoint/errors", "GET")]
        [TestCase("/api/endpoints/no-such-endpoint/messages/search?q=anything", "GET")]
        [TestCase("/api/endpoints/no-such-endpoint/messages/search/anything", "GET")]
        [TestCase("/api/errors/groups", "GET")]
        [TestCase("/api/endpoints", "GET")]
        [TestCase("/api/heartbeats/stats", "GET")]
        [TestCase("/api/recoverability/classifiers", "GET")]
        [TestCase("/api/recoverability/groups", "GET")]
        [TestCase("/api/recoverability/history", "GET")]
        // A group that does not exist still answers, with an empty page and a validator of its own.
        [TestCase("/api/recoverability/groups/no-such-group/errors", "GET")]
        [TestCase("/api/recoverability/groups/no-such-group/errors", "HEAD")]
        [TestCase("/api/conversations/no-such-conversation", "GET")]
        public async Task Should_answer_not_modified(string url, string method)
        {
            Answer issued = null;
            Answer repeated = null;

            await Define<Context>()
                .Done(async ctx =>
                {
                    // Internal custom checks re-report on a timer, so the validator can move between
                    // the two requests.
                    for (var attempt = 0; attempt < 5; attempt++)
                    {
                        issued = await Ask(method, url, ifNoneMatch: null);

                        if (issued.Etag == null)
                        {
                            continue;
                        }

                        repeated = await Ask(method, url, issued.Etag);

                        if (repeated.Status == HttpStatusCode.NotModified)
                        {
                            break;
                        }
                    }

                    return true;
                })
                .Run();

            Assert.That(issued.Etag, Is.Not.Null, $"{method} {url} issued no ETag, so there is nothing for a client to revalidate against");
            Assert.That(repeated.Status, Is.EqualTo(HttpStatusCode.NotModified), $"{method} {url} sent the full payload again for a client that already held {issued.Etag}");

            // ServicePulse drives pagination from Total-Count, and a revalidating client takes it from
            // the 304 rather than from the body it already holds.
            Assert.That(repeated.TotalCount, Is.Not.Null.And.EqualTo(issued.TotalCount), $"{method} {url} did not carry its Total-Count through to the 304");
        }

        [Test]
        public async Task Should_answer_with_a_new_etag_once_the_data_moves()
        {
            Answer before = null;
            Answer after = null;

            await Define<Context>()
                .Done(async ctx =>
                {
                    before = await Ask("GET", "/api/redirects", ifNoneMatch: null);

                    if (before.Etag == null)
                    {
                        return false;
                    }

                    using var created = await HttpClient.PostAsJsonAsync("/api/redirects", new
                    {
                        FromPhysicalAddress = "SomeEndpoint@MACHINE",
                        ToPhysicalAddress = "OtherEndpoint@MACHINE"
                    });

                    created.EnsureSuccessStatusCode();

                    after = await Ask("GET", "/api/redirects", before.Etag);

                    return true;
                })
                .Run();

            Assert.That(after.Status, Is.EqualTo(HttpStatusCode.OK),
                "a redirect was added, so the client's validator is stale and it has to be sent the new list");
            Assert.That(after.Etag, Is.Not.Null.And.Not.EqualTo(before.Etag),
                "the body changed, so the validator has to move with it or the next poll caches the stale list forever");
        }

        async Task<Answer> Ask(string method, string url, string ifNoneMatch)
        {
            using var response = await Send(method, url, ifNoneMatch);

            return new Answer(
                response.StatusCode,
                Header(response, "ETag"),
                Header(response, "Total-Count"));
        }

        static string Header(HttpResponseMessage response, string name) =>
            response.Headers.TryGetValues(name, out var values) ? string.Join(string.Empty, values) : null;

        Task<HttpResponseMessage> Send(string method, string url, string ifNoneMatch)
        {
            var request = new HttpRequestMessage(new HttpMethod(method), url);

            if (ifNoneMatch != null)
            {
                // Unvalidated, so a malformed validator reaches the server and shows up as a 200
                // rather than throwing here.
                request.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);
            }

            return HttpClient.SendAsync(request);
        }

        record Answer(HttpStatusCode Status, string Etag, string TotalCount);

        class Context : ScenarioContext;
    }
}
