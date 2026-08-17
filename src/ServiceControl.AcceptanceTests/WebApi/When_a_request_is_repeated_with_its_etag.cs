namespace ServiceControl.AcceptanceTests.WebApi
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using Recoverability.MessageRedirects;

    class When_a_request_is_repeated_with_its_etag : AcceptanceTest
    {
        [TestCase("/api/customchecks", "GET", false)]
        [TestCase("/api/redirects", "GET", true)]
        [TestCase("/api/redirect", "HEAD", true)]
        public async Task Should_answer_not_modified(string url, string method, bool seedARedirect)
        {
            Answer issued = null;
            Answer repeated = null;

            await Define<Context>()
                .Done(async ctx =>
                {
                    if (seedARedirect)
                    {
                        await this.Post("/api/redirects", new RedirectRequest
                        {
                            fromphysicaladdress = "endpointA@machine1",
                            tophysicaladdress = "endpointB@machine2"
                        }, status => status is not HttpStatusCode.Created);
                    }

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
