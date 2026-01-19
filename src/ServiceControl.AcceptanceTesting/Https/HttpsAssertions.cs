namespace ServiceControl.AcceptanceTesting.Https
{
    using System.Net;
    using System.Net.Http;
    using NUnit.Framework;

    /// <summary>
    /// Shared assertion helpers for HTTPS acceptance tests.
    /// Used across all instance types (Primary, Audit, Monitoring).
    /// </summary>
    public static class HttpsAssertions
    {
        public const string StrictTransportSecurityHeader = "Strict-Transport-Security";
        public const string LocationHeader = "Location";

        /// <summary>
        /// Asserts that the response is a redirect to HTTPS.
        /// </summary>
        public static void AssertHttpsRedirect(HttpResponseMessage response, int? expectedPort = null)
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.RedirectKeepVerb).Or.EqualTo(HttpStatusCode.Redirect),
                            "Response should be a redirect (307 or 302)");

                Assert.That(response.Headers.Location, Is.Not.Null,
                    "Response should contain Location header");
            }

            var locationUri = response.Headers.Location;
            Assert.That(locationUri.Scheme, Is.EqualTo("https"),
                "Redirect Location should use HTTPS scheme");

            if (expectedPort.HasValue)
            {
                Assert.That(locationUri.Port, Is.EqualTo(expectedPort.Value),
                    $"Redirect Location should use port {expectedPort.Value}");
            }
        }

        /// <summary>
        /// Asserts that the response is NOT a redirect (for when HTTPS redirect is disabled).
        /// </summary>
        public static void AssertNoHttpsRedirect(HttpResponseMessage response)
        {
            Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.RedirectKeepVerb).And.Not.EqualTo(HttpStatusCode.Redirect),
                "Response should not be a redirect when HTTPS redirect is disabled");
        }

        /// <summary>
        /// Asserts that the response contains the HSTS header with expected values.
        /// </summary>
        public static void AssertHstsHeader(HttpResponseMessage response, int expectedMaxAge = 31536000, bool expectIncludeSubDomains = false)
        {
            Assert.That(response.Headers.Contains(StrictTransportSecurityHeader), Is.True,
                "Response should contain Strict-Transport-Security header");

            var hstsValue = string.Join("; ", response.Headers.GetValues(StrictTransportSecurityHeader));

            Assert.That(hstsValue, Does.Contain($"max-age={expectedMaxAge}"),
                $"HSTS header should contain max-age={expectedMaxAge}");

            if (expectIncludeSubDomains)
            {
                Assert.That(hstsValue, Does.Contain("includeSubDomains"),
                    "HSTS header should contain includeSubDomains");
            }
        }

        /// <summary>
        /// Asserts that the response does NOT contain the HSTS header.
        /// </summary>
        public static void AssertNoHstsHeader(HttpResponseMessage response)
        {
            Assert.That(response.Headers.Contains(StrictTransportSecurityHeader), Is.False,
                "Response should not contain Strict-Transport-Security header when HSTS is disabled");
        }
    }
}
