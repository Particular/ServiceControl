namespace ServiceControl.Audit.AcceptanceTests.Security.ForwardedHeaders
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.ForwardedHeaders;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// Unknown Network Rejected
    /// When KnownNetworks are configured but the caller IP does NOT fall within, headers should be ignored.
    /// </summary>
    class When_unknown_network_sends_headers : AcceptanceTest
    {
        ForwardedHeadersTestConfiguration configuration;

        [SetUp]
        public void ConfigureForwardedHeaders() =>
            // Configure known networks that do NOT include localhost (test server uses localhost)
            // This should cause headers to be ignored
            configuration = new ForwardedHeadersTestConfiguration(ServiceControlInstanceType.Audit)
                .WithKnownNetworks("10.0.0.0/8,192.168.0.0/16");

        [TearDown]
        public void CleanupForwardedHeaders() => configuration?.Dispose();

        [Test]
        public async Task Headers_should_be_ignored_when_caller_not_in_known_networks()
        {
            RequestInfoResponse requestInfo = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    // Simulate request from IP 203.0.113.1 (TEST-NET-3, not in known networks)
                    // The known networks are 10.0.0.0/8 and 192.168.0.0/16, so this IP should be rejected
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

            // Verify configuration shows the networks (203.0.113.1 is NOT in these networks)
            Assert.That(requestInfo.Configuration.KnownNetworks, Does.Contain("10.0.0.0/8"));
            Assert.That(requestInfo.Configuration.KnownNetworks, Does.Contain("192.168.0.0/16"));
        }

        class Context : ScenarioContext
        {
        }
    }
}
