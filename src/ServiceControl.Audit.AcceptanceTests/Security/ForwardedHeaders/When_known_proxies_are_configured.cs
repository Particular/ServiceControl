namespace ServiceControl.Audit.AcceptanceTests.Security.ForwardedHeaders
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.ForwardedHeaders;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// Tests Scenario 3: Known Proxies Only from local-forward-headers-testing.md
    /// When KnownProxies are configured and the caller IP matches, headers should be applied.
    /// </summary>
    class When_known_proxies_are_configured : AcceptanceTest
    {
        ForwardedHeadersTestConfiguration configuration;

        [SetUp]
        public void ConfigureForwardedHeaders()
        {
            // Configure known proxies to include localhost addresses (test server uses localhost)
            configuration = new ForwardedHeadersTestConfiguration(ServiceControlInstanceType.Audit)
                .WithKnownProxies("127.0.0.1,::1");
        }

        [TearDown]
        public void CleanupForwardedHeaders()
        {
            configuration?.Dispose();
        }

        [Test]
        public async Task Headers_should_be_applied_when_caller_matches_known_proxy()
        {
            RequestInfoResponse requestInfo = null;

            await Define<Context>()
                .Done(async ctx =>
                {
                    requestInfo = await ForwardedHeadersAssertions.GetRequestInfo(
                        HttpClient,
                        SerializerOptions,
                        xForwardedFor: "203.0.113.50",
                        xForwardedProto: "https",
                        xForwardedHost: "example.com");
                    return requestInfo != null;
                })
                .Run();

            ForwardedHeadersAssertions.AssertHeadersAppliedWithKnownProxiesOrNetworks(
                requestInfo,
                expectedScheme: "https",
                expectedHost: "example.com",
                expectedRemoteIp: "203.0.113.50");

            // Verify configuration shows known proxies
            Assert.That(requestInfo.Configuration.KnownProxies, Does.Contain("127.0.0.1").Or.Contain("::1"));
        }

        class Context : ScenarioContext
        {
        }
    }
}
