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
                request.Headers.Add(TestRemoteIpMiddleware.HeaderName, testRemoteIp);
            }

            var response = await httpClient.SendAsync(request);
            _ = response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<RequestInfoResponse>(content, serializerOptions);
        }

        /// <summary>
        /// Direct Access (No Proxy)
        /// When no forwarded headers are sent, the request values should remain unchanged.
        /// </summary>
        public static void AssertDirectAccessWithNoForwardedHeaders(RequestInfoResponse requestInfo)
        {
            Assert.That(requestInfo, Is.Not.Null);

            using (Assert.EnterMultipleScope())
            {
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
        }

        /// <summary>
        /// Default Behavior with Headers (TrustAllProxies = true)
        /// When forwarded headers are sent and all proxies are trusted, headers should be applied.
        /// </summary>
        public static void AssertHeadersAppliedWhenTrustAllProxies(
            RequestInfoResponse requestInfo,
            string expectedScheme,
            string expectedHost,
            string expectedRemoteIp)
        {
            Assert.That(requestInfo, Is.Not.Null);

            using (Assert.EnterMultipleScope())
            {
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
        }

        /// <summary>
        /// Partial Headers (Proto Only)
        /// When only X-Forwarded-Proto is sent, only scheme should change.
        /// </summary>
        public static void AssertPartialHeadersApplied(
            RequestInfoResponse requestInfo,
            string expectedScheme)
        {
            Assert.That(requestInfo, Is.Not.Null);

            using (Assert.EnterMultipleScope())
            {
                // Only scheme should be changed
                Assert.That(requestInfo.Processed.Scheme, Is.EqualTo(expectedScheme));

                // Host should remain original (not changed to a forwarded value)
                // In test environment this will be the test server host, not a forwarded host like "example.com"
                Assert.That(requestInfo.Processed.Host, Is.Not.Null.And.Not.Empty);
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(requestInfo.Processed.Host, Does.Not.Contain("example.com"));

                // RemoteIpAddress should NOT be a forwarded IP (203.0.113.50)
                // In test server environment it may be null, localhost, or machine-specific
                Assert.That(requestInfo.Processed.RemoteIpAddress, Is.Null.Or.Not.EqualTo("203.0.113.50"));

                // Configuration should show trust all proxies
                Assert.That(requestInfo.Configuration.Enabled, Is.True);
                Assert.That(requestInfo.Configuration.TrustAllProxies, Is.True);
            }
        }

        /// <summary>
        /// Proxy Chain (Multiple X-Forwarded-For Values)
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

            using (Assert.EnterMultipleScope())
            {
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
        }

        /// <summary>
        /// Headers Applied with Known Proxies/Networks
        /// When the caller IP matches KnownProxies or KnownNetworks, headers should be applied.
        /// </summary>
        public static void AssertHeadersAppliedWithKnownProxiesOrNetworks(
            RequestInfoResponse requestInfo,
            string expectedScheme,
            string expectedHost,
            string expectedRemoteIp)
        {
            Assert.That(requestInfo, Is.Not.Null);

            using (Assert.EnterMultipleScope())
            {
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
        }

        /// <summary>
        /// Headers Ignored when Proxy/Network Not Trusted
        /// When the caller IP does NOT match KnownProxies or KnownNetworks, headers should be ignored.
        /// </summary>
        public static void AssertHeadersIgnoredWhenProxyNotTrusted(
            RequestInfoResponse requestInfo,
            string sentXForwardedFor,
            string sentXForwardedProto,
            string sentXForwardedHost)
        {
            Assert.That(requestInfo, Is.Not.Null);

            using (Assert.EnterMultipleScope())
            {
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
        }

        /// <summary>
        /// Forwarded Headers Disabled
        /// When forwarded headers processing is disabled, headers should be ignored regardless of trust.
        /// </summary>
        public static void AssertHeadersIgnoredWhenDisabled(
            RequestInfoResponse requestInfo,
            string sentXForwardedFor,
            string sentXForwardedProto,
            string sentXForwardedHost)
        {
            Assert.That(requestInfo, Is.Not.Null);

            using (Assert.EnterMultipleScope())
            {
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
        }

        /// <summary>
        /// Proxy Chain with ForwardLimit=1 (Known Proxies)
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

            using (Assert.EnterMultipleScope())
            {
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

        /// <summary>
        /// Multiple Header Values with TrustAllProxies=true
        /// When TrustAllProxies is true and multiple values are in X-Forwarded-Proto/Host,
        /// the original (leftmost) values should be returned.
        /// </summary>
        public static void AssertMultipleHeaderValuesProcessedWithTrustAllProxies(
            RequestInfoResponse requestInfo,
            string expectedOriginalScheme,
            string expectedOriginalHost,
            string expectedOriginalClientIp)
        {
            Assert.That(requestInfo, Is.Not.Null);

            using (Assert.EnterMultipleScope())
            {
                // When TrustAllProxies=true, ForwardLimit=null, so middleware processes all values
                // and returns the original (leftmost) values
                Assert.That(requestInfo.Processed.Scheme, Is.EqualTo(expectedOriginalScheme));
                Assert.That(requestInfo.Processed.Host, Is.EqualTo(expectedOriginalHost));
                Assert.That(requestInfo.Processed.RemoteIpAddress, Is.EqualTo(expectedOriginalClientIp));

                // Raw headers should be empty because middleware consumed them
                Assert.That(requestInfo.RawHeaders.XForwardedFor, Is.Empty);
                Assert.That(requestInfo.RawHeaders.XForwardedProto, Is.Empty);
                Assert.That(requestInfo.RawHeaders.XForwardedHost, Is.Empty);

                // Configuration should show trust all proxies
                Assert.That(requestInfo.Configuration.Enabled, Is.True);
                Assert.That(requestInfo.Configuration.TrustAllProxies, Is.True);
            }
        }

        /// <summary>
        /// Multiple Header Values with ForwardLimit=1 (Known Proxies)
        /// When TrustAllProxies=false, ForwardLimit=1, so only the rightmost values are processed.
        /// </summary>
        public static void AssertMultipleHeaderValuesWithForwardLimitOne(
            RequestInfoResponse requestInfo,
            string expectedLastScheme,
            string expectedLastHost,
            string expectedLastProxyIp,
            string expectedRemainingForwardedFor,
            string expectedRemainingForwardedProto,
            string expectedRemainingForwardedHost)
        {
            Assert.That(requestInfo, Is.Not.Null);

            using (Assert.EnterMultipleScope())
            {
                // When TrustAllProxies=false, ForwardLimit=1, so only rightmost values are processed
                Assert.That(requestInfo.Processed.Scheme, Is.EqualTo(expectedLastScheme));
                Assert.That(requestInfo.Processed.Host, Is.EqualTo(expectedLastHost));
                Assert.That(requestInfo.Processed.RemoteIpAddress, Is.EqualTo(expectedLastProxyIp));

                // Raw headers should contain remaining values (not fully consumed)
                Assert.That(requestInfo.RawHeaders.XForwardedFor, Is.EqualTo(expectedRemainingForwardedFor));
                Assert.That(requestInfo.RawHeaders.XForwardedProto, Is.EqualTo(expectedRemainingForwardedProto));
                Assert.That(requestInfo.RawHeaders.XForwardedHost, Is.EqualTo(expectedRemainingForwardedHost));

                // Configuration should show TrustAllProxies=false
                Assert.That(requestInfo.Configuration.Enabled, Is.True);
                Assert.That(requestInfo.Configuration.TrustAllProxies, Is.False);
            }
        }
    }
}
