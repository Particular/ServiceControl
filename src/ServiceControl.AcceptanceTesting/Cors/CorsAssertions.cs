namespace ServiceControl.AcceptanceTesting.Cors
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using NUnit.Framework;

    /// <summary>
    /// Shared assertion helpers for CORS acceptance tests.
    /// Used across all instance types (Primary, Audit, Monitoring).
    /// </summary>
    public static class CorsAssertions
    {
        public const string AllowOriginHeader = "Access-Control-Allow-Origin";
        public const string AllowCredentialsHeader = "Access-Control-Allow-Credentials";
        public const string AllowMethodsHeader = "Access-Control-Allow-Methods";
        public const string AllowHeadersHeader = "Access-Control-Allow-Headers";
        public const string ExposeHeadersHeader = "Access-Control-Expose-Headers";

        /// <summary>
        /// Sends a preflight OPTIONS request with the Origin header to check CORS policy.
        /// </summary>
        public static async Task<HttpResponseMessage> SendPreflightRequest(
            HttpClient httpClient,
            string origin,
            string requestMethod = "GET")
        {
            using var request = new HttpRequestMessage(HttpMethod.Options, "/api");
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", requestMethod);

            return await httpClient.SendAsync(request);
        }

        /// <summary>
        /// Sends a simple GET request with the Origin header to check CORS response headers.
        /// </summary>
        public static async Task<HttpResponseMessage> SendRequestWithOrigin(
            HttpClient httpClient,
            string origin,
            string endpoint = "/api")
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Add("Origin", origin);

            return await httpClient.SendAsync(request);
        }

        /// <summary>
        /// Asserts that CORS is configured to allow any origin.
        /// The Access-Control-Allow-Origin header should be "*".
        /// </summary>
        public static void AssertAllowAnyOrigin(HttpResponseMessage response, string sentOrigin)
        {
            Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.Forbidden));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.Headers.Contains(AllowOriginHeader), Is.True,
                    "Response should contain Access-Control-Allow-Origin header");

                var allowOrigin = response.Headers.GetValues(AllowOriginHeader);
                Assert.That(allowOrigin, Does.Contain("*"),
                    "Access-Control-Allow-Origin should be '*' when AllowAnyOrigin is true");

                // When AllowAnyOrigin is true, AllowCredentials should NOT be set
                // (browsers reject credentials with wildcard origin)
                Assert.That(response.Headers.Contains(AllowCredentialsHeader), Is.False,
                    "Access-Control-Allow-Credentials should not be set with wildcard origin");
            }
        }

        /// <summary>
        /// Asserts that CORS allows the specific origin that was requested.
        /// </summary>
        public static void AssertAllowedOrigin(HttpResponseMessage response, string expectedOrigin)
        {
            Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.Forbidden));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.Headers.Contains(AllowOriginHeader), Is.True,
                    "Response should contain Access-Control-Allow-Origin header");

                var allowOrigin = response.Headers.GetValues(AllowOriginHeader);
                Assert.That(allowOrigin, Does.Contain(expectedOrigin),
                    $"Access-Control-Allow-Origin should be '{expectedOrigin}'");

                // When specific origins are configured, AllowCredentials should be true
                Assert.That(response.Headers.Contains(AllowCredentialsHeader), Is.True,
                    "Access-Control-Allow-Credentials should be set for specific origins");

                var allowCredentials = response.Headers.GetValues(AllowCredentialsHeader);
                Assert.That(allowCredentials, Does.Contain("true"),
                    "Access-Control-Allow-Credentials should be 'true'");
            }
        }

        /// <summary>
        /// Asserts that CORS does not allow the requested origin.
        /// The Access-Control-Allow-Origin header should NOT be present or should not match the sent origin.
        /// </summary>
        public static void AssertOriginNotAllowed(HttpResponseMessage response, string sentOrigin)
        {
            // The request may succeed (200 OK) but without CORS headers,
            // browsers will block the response from being read by JavaScript
            using (Assert.EnterMultipleScope())
            {
                if (response.Headers.Contains(AllowOriginHeader))
                {
                    var allowOrigin = string.Join(", ", response.Headers.GetValues(AllowOriginHeader));
                    Assert.That(allowOrigin, Is.Not.EqualTo(sentOrigin).And.Not.EqualTo("*"),
                        $"Access-Control-Allow-Origin should not allow '{sentOrigin}'");
                }
                // If header is not present at all, that's also valid for "not allowed"
            }
        }

        /// <summary>
        /// Asserts that CORS is completely disabled (no origins allowed).
        /// </summary>
        public static void AssertCorsDisabled(HttpResponseMessage response)
        {
            // When CORS is disabled, no Access-Control-Allow-Origin header should be present
            Assert.That(response.Headers.Contains(AllowOriginHeader), Is.False,
                "Access-Control-Allow-Origin header should not be present when CORS is disabled");
        }

        /// <summary>
        /// Asserts that the CORS preflight response includes expected methods.
        /// </summary>
        public static void AssertAllowedMethods(HttpResponseMessage response, params string[] expectedMethods)
        {
            Assert.That(response.Headers.Contains(AllowMethodsHeader), Is.True,
                "Response should contain Access-Control-Allow-Methods header");

            var allowMethods = string.Join(", ", response.Headers.GetValues(AllowMethodsHeader));
            foreach (var method in expectedMethods)
            {
                Assert.That(allowMethods, Does.Contain(method),
                    $"Access-Control-Allow-Methods should contain '{method}'");
            }
        }

        /// <summary>
        /// Asserts that the CORS response exposes expected headers.
        /// </summary>
        public static void AssertExposedHeaders(HttpResponseMessage response, params string[] expectedHeaders)
        {
            Assert.That(response.Headers.Contains(ExposeHeadersHeader), Is.True,
                "Response should contain Access-Control-Expose-Headers header");

            var exposeHeaders = string.Join(", ", response.Headers.GetValues(ExposeHeadersHeader));
            foreach (var header in expectedHeaders)
            {
                Assert.That(exposeHeaders, Does.Contain(header),
                    $"Access-Control-Expose-Headers should contain '{header}'");
            }
        }
    }
}
