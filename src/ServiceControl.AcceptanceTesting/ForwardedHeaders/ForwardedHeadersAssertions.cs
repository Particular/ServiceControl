namespace ServiceControl.AcceptanceTesting.ForwardedHeaders
{
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using NUnit.Framework;

    /// <summary>
    /// Shared assertion helpers for forwarded headers acceptance tests.
    /// Used across all instance types (Primary, Audit, Monitoring).
    /// </summary>
    public static class ForwardedHeadersAssertions
    {
        /// <summary>
        /// Fetches request info from the debug endpoint with optional custom forwarded headers.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use</param>
        /// <param name="serializerOptions">JSON serializer options</param>
        /// <param name="xForwardedFor">X-Forwarded-For header value</param>
        /// <param name="xForwardedProto">X-Forwarded-Proto header value</param>
        /// <param name="xForwardedHost">X-Forwarded-Host header value</param>
        /// <param name="testRemoteIp">Test-only: Simulates the request coming from this IP address.
        /// Used to test ForwardedHeaders behavior with KnownProxies/KnownNetworks configurations.</param>
        public static async Task<RequestInfoResponse> GetRequestInfo(
            HttpClient httpClient,
            JsonSerializerOptions serializerOptions,
            string xForwardedFor = null,
            string xForwardedProto = null,
            string xForwardedHost = null,
            string testRemoteIp = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/debug/request-info");

            if (!string.IsNullOrEmpty(xForwardedFor))
            {
                request.Headers.Add("X-Forwarded-For", xForwardedFor);
            }
            if (!string.IsNullOrEmpty(xForwardedProto))
            {
                request.Headers.Add("X-Forwarded-Proto", xForwardedProto);
            }
            if (!string.IsNullOrEmpty(xForwardedHost))
            {
                request.Headers.Add("X-Forwarded-Host", xForwardedHost);
            }
            if (!string.IsNullOrEmpty(testRemoteIp))
            {
                request.Headers.Add("X-Test-Remote-IP", testRemoteIp);
            }

            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<RequestInfoResponse>(content, serializerOptions);
        }

        /// <summary>
        /// Asserts Scenario 0: Direct Access (No Proxy)
        /// When no forwarded headers are sent, the request values should remain unchanged.
        /// </summary>
        public static void AssertDirectAccessWithNoForwardedHeaders(RequestInfoResponse requestInfo)
        {
            Assert.That(requestInfo, Is.Not.Null);

            // Processed values should reflect the direct request (no proxy transformation)
            Assert.That(requestInfo.Processed.Scheme, Is.EqualTo("http"));
            Assert.That(requestInfo.Processed.Host, Is.Not.Null.And.Not.Empty);

            // Raw headers should be empty since no forwarded headers were sent
            Assert.That(requestInfo.RawHeaders.XForwardedFor, Is.Empty);
            Assert.That(requestInfo.RawHeaders.XForwardedProto, Is.Empty);
            Assert.That(requestInfo.RawHeaders.XForwardedHost, Is.Empty);

            // Default configuration: enabled with trust all proxies
            Assert.That(requestInfo.Configuration.Enabled, Is.True);
            Assert.That(requestInfo.Configuration.TrustAllProxies, Is.True);
        }

        /// <summary>
        /// Asserts Scenario 1/2: Default Behavior with Headers (TrustAllProxies = true)
        /// When forwarded headers are sent and all proxies are trusted, headers should be applied.
        /// </summary>
        public static void AssertHeadersAppliedWhenTrustAllProxies(
            RequestInfoResponse requestInfo,
            string expectedScheme,
            string expectedHost,
            string expectedRemoteIp)
        {
            Assert.That(requestInfo, Is.Not.Null);

            // Processed values should reflect the forwarded headers
            Assert.That(requestInfo.Processed.Scheme, Is.EqualTo(expectedScheme));
            Assert.That(requestInfo.Processed.Host, Is.EqualTo(expectedHost));
            Assert.That(requestInfo.Processed.RemoteIpAddress, Is.EqualTo(expectedRemoteIp));

            // Raw headers should be empty because middleware consumed them
            Assert.That(requestInfo.RawHeaders.XForwardedFor, Is.Empty);
            Assert.That(requestInfo.RawHeaders.XForwardedProto, Is.Empty);
            Assert.That(requestInfo.RawHeaders.XForwardedHost, Is.Empty);

            // Configuration should show trust all proxies
            Assert.That(requestInfo.Configuration.Enabled, Is.True);
            Assert.That(requestInfo.Configuration.TrustAllProxies, Is.True);
        }

        /// <summary>
        /// Asserts Scenario 11: Partial Headers (Proto Only)
        /// When only X-Forwarded-Proto is sent, only scheme should change.
        /// </summary>
        public static void AssertPartialHeadersApplied(
            RequestInfoResponse requestInfo,
            string expectedScheme)
        {
            Assert.That(requestInfo, Is.Not.Null);

            // Only scheme should be changed
            Assert.That(requestInfo.Processed.Scheme, Is.EqualTo(expectedScheme));

            // Host should remain original (not changed to a forwarded value)
            // In test environment this will be the test server host, not a forwarded host like "example.com"
            Assert.That(requestInfo.Processed.Host, Is.Not.Null.And.Not.Empty);
            Assert.That(requestInfo.Processed.Host, Does.Not.Contain("example.com"));

            // RemoteIpAddress should NOT be a forwarded IP (203.0.113.50)
            // In test server environment it may be null, localhost, or machine-specific
            Assert.That(requestInfo.Processed.RemoteIpAddress, Is.Null.Or.Not.EqualTo("203.0.113.50"));

            // Configuration should show trust all proxies
            Assert.That(requestInfo.Configuration.Enabled, Is.True);
            Assert.That(requestInfo.Configuration.TrustAllProxies, Is.True);
        }

        /// <summary>
        /// Asserts Scenario 8: Proxy Chain (Multiple X-Forwarded-For Values)
        /// When TrustAllProxies is true and multiple IPs are in X-Forwarded-For,
        /// the original client IP (first in the chain) should be returned.
        /// </summary>
        public static void AssertProxyChainProcessedWithTrustAllProxies(
            RequestInfoResponse requestInfo,
            string expectedOriginalClientIp,
            string expectedScheme,
            string expectedHost)
        {
            Assert.That(requestInfo, Is.Not.Null);

            // When TrustAllProxies=true, ForwardLimit=null, so middleware processes all IPs
            // and returns the original client IP (first in the chain)
            Assert.That(requestInfo.Processed.Scheme, Is.EqualTo(expectedScheme));
            Assert.That(requestInfo.Processed.Host, Is.EqualTo(expectedHost));
            Assert.That(requestInfo.Processed.RemoteIpAddress, Is.EqualTo(expectedOriginalClientIp));

            // Raw headers should be empty because middleware consumed them
            Assert.That(requestInfo.RawHeaders.XForwardedFor, Is.Empty);
            Assert.That(requestInfo.RawHeaders.XForwardedProto, Is.Empty);
            Assert.That(requestInfo.RawHeaders.XForwardedHost, Is.Empty);

            // Configuration should show trust all proxies
            Assert.That(requestInfo.Configuration.Enabled, Is.True);
            Assert.That(requestInfo.Configuration.TrustAllProxies, Is.True);
        }

        /// <summary>
        /// Asserts Scenario 3/4/10: Headers Applied with Known Proxies/Networks
        /// When the caller IP matches KnownProxies or KnownNetworks, headers should be applied.
        /// </summary>
        public static void AssertHeadersAppliedWithKnownProxiesOrNetworks(
            RequestInfoResponse requestInfo,
            string expectedScheme,
            string expectedHost,
            string expectedRemoteIp)
        {
            Assert.That(requestInfo, Is.Not.Null);

            // Headers should be applied because caller is trusted
            Assert.That(requestInfo.Processed.Scheme, Is.EqualTo(expectedScheme));
            Assert.That(requestInfo.Processed.Host, Is.EqualTo(expectedHost));
            Assert.That(requestInfo.Processed.RemoteIpAddress, Is.EqualTo(expectedRemoteIp));

            // Raw headers should be empty because middleware consumed them
            Assert.That(requestInfo.RawHeaders.XForwardedFor, Is.Empty);
            Assert.That(requestInfo.RawHeaders.XForwardedProto, Is.Empty);
            Assert.That(requestInfo.RawHeaders.XForwardedHost, Is.Empty);

            // Configuration should show TrustAllProxies=false (auto-disabled when proxies/networks configured)
            Assert.That(requestInfo.Configuration.Enabled, Is.True);
            Assert.That(requestInfo.Configuration.TrustAllProxies, Is.False);
        }

        /// <summary>
        /// Asserts Scenario 5/6: Headers Ignored when Proxy/Network Not Trusted
        /// When the caller IP does NOT match KnownProxies or KnownNetworks, headers should be ignored.
        /// </summary>
        public static void AssertHeadersIgnoredWhenProxyNotTrusted(
            RequestInfoResponse requestInfo,
            string sentXForwardedFor,
            string sentXForwardedProto,
            string sentXForwardedHost)
        {
            Assert.That(requestInfo, Is.Not.Null);

            // Headers should NOT be applied - values should remain unchanged from direct request
            Assert.That(requestInfo.Processed.Scheme, Is.EqualTo("http"));
            // Host should remain the test server host, not the forwarded host
            Assert.That(requestInfo.Processed.Host, Does.Not.Contain("example.com"));

            // Raw headers should still contain the sent values (not consumed by middleware)
            Assert.That(requestInfo.RawHeaders.XForwardedFor, Is.EqualTo(sentXForwardedFor));
            Assert.That(requestInfo.RawHeaders.XForwardedProto, Is.EqualTo(sentXForwardedProto));
            Assert.That(requestInfo.RawHeaders.XForwardedHost, Is.EqualTo(sentXForwardedHost));

            // Configuration should show TrustAllProxies=false
            Assert.That(requestInfo.Configuration.Enabled, Is.True);
            Assert.That(requestInfo.Configuration.TrustAllProxies, Is.False);
        }

        /// <summary>
        /// Asserts Scenario 7: Forwarded Headers Disabled
        /// When forwarded headers processing is disabled, headers should be ignored regardless of trust.
        /// </summary>
        public static void AssertHeadersIgnoredWhenDisabled(
            RequestInfoResponse requestInfo,
            string sentXForwardedFor,
            string sentXForwardedProto,
            string sentXForwardedHost)
        {
            Assert.That(requestInfo, Is.Not.Null);

            // Headers should NOT be applied - values should remain unchanged
            Assert.That(requestInfo.Processed.Scheme, Is.EqualTo("http"));
            Assert.That(requestInfo.Processed.Host, Does.Not.Contain("example.com"));

            // Raw headers should still contain the sent values (middleware disabled)
            Assert.That(requestInfo.RawHeaders.XForwardedFor, Is.EqualTo(sentXForwardedFor));
            Assert.That(requestInfo.RawHeaders.XForwardedProto, Is.EqualTo(sentXForwardedProto));
            Assert.That(requestInfo.RawHeaders.XForwardedHost, Is.EqualTo(sentXForwardedHost));

            // Configuration should show Enabled=false
            Assert.That(requestInfo.Configuration.Enabled, Is.False);
        }

        /// <summary>
        /// Asserts Scenario 9: Proxy Chain with ForwardLimit=1 (Known Proxies)
        /// When TrustAllProxies=false, ForwardLimit=1, so only the last proxy IP is processed.
        /// </summary>
        public static void AssertProxyChainWithForwardLimitOne(
            RequestInfoResponse requestInfo,
            string expectedLastProxyIp,
            string expectedScheme,
            string expectedHost,
            string expectedRemainingForwardedFor)
        {
            Assert.That(requestInfo, Is.Not.Null);

            // When TrustAllProxies=false, ForwardLimit=1, so only last IP is processed
            Assert.That(requestInfo.Processed.Scheme, Is.EqualTo(expectedScheme));
            Assert.That(requestInfo.Processed.Host, Is.EqualTo(expectedHost));
            Assert.That(requestInfo.Processed.RemoteIpAddress, Is.EqualTo(expectedLastProxyIp));

            // X-Forwarded-For should contain remaining IPs (not fully consumed)
            Assert.That(requestInfo.RawHeaders.XForwardedFor, Is.EqualTo(expectedRemainingForwardedFor));
            // Proto and Host are fully consumed
            Assert.That(requestInfo.RawHeaders.XForwardedProto, Is.Empty);
            Assert.That(requestInfo.RawHeaders.XForwardedHost, Is.Empty);

            // Configuration should show TrustAllProxies=false
            Assert.That(requestInfo.Configuration.Enabled, Is.True);
            Assert.That(requestInfo.Configuration.TrustAllProxies, Is.False);
        }
    }
}
