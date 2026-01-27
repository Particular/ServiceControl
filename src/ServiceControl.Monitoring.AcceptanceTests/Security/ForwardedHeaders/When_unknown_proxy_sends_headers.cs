namespace ServiceControl.Monitoring.AcceptanceTests.Security.ForwardedHeaders
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.ForwardedHeaders;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// When KnownProxies are configured but the caller IP does NOT match, headers should be ignored.
    /// </summary>
    class When_unknown_proxy_sends_headers : AcceptanceTest
    {
        ForwardedHeadersTestConfiguration configuration;

        [SetUp]
        public void ConfigureForwardedHeaders() =>
            // Configure a known proxy that does NOT match localhost (test server uses localhost)
            // This should cause headers to be ignored
            configuration = new ForwardedHeadersTestConfiguration(ServiceControlInstanceType.Monitoring)
                .WithKnownProxies("192.168.1.100");

        [TearDown]
        public void CleanupForwardedHeaders() => configuration?.Dispose();

        [Test]
        public async Task Headers_should_be_ignored_when_caller_not_in_known_proxies()
        {
            RequestInfoResponse requestInfo = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    // Simulate request from IP 203.0.113.1 (TEST-NET-3, not in known proxies)
                    // The known proxy is 192.168.1.100, so this IP should be rejected
                    requestInfo = await ForwardedHeadersAssertions.GetRequestInfo(
                        HttpClient,
                        SerializerOptions,
                        xForwardedFor: "203.0.113.50",
                        xForwardedProto: "https",
                        xForwardedHost: "example.com",
                        testRemoteIp: "203.0.113.1");
                    return requestInfo != null;
                })
                .Run();

            ForwardedHeadersAssertions.AssertHeadersIgnoredWhenProxyNotTrusted(
                requestInfo,
                sentXForwardedFor: "203.0.113.50",
                sentXForwardedProto: "https",
                sentXForwardedHost: "example.com");

            // Verify configuration shows the trusted proxy (203.0.113.1 is NOT this proxy)
            Assert.That(requestInfo.Configuration.KnownProxies, Does.Contain("192.168.1.100"));
        }

        class Context : ScenarioContext
        {
        }
    }
}
