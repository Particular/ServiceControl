namespace ServiceControl.AcceptanceTests.Security.ForwardedHeaders
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.ForwardedHeaders;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// Tests Scenario 10: Combined Known Proxies and Networks from local-forward-headers-testing.md
    /// When both KnownProxies and KnownNetworks are configured, matching either grants trust.
    /// </summary>
    class When_combined_proxies_and_networks_are_configured : AcceptanceTest
    {
        ForwardedHeadersTestConfiguration configuration;

        [SetUp]
        public void ConfigureForwardedHeaders()
        {
            // Configure both proxies (that don't match localhost) and networks (that include localhost)
            // The localhost should match via the networks, proving OR logic
            configuration = new ForwardedHeadersTestConfiguration(ServiceControlInstanceType.Primary)
                .WithKnownProxiesAndNetworks("192.168.1.100", "127.0.0.0/8,::1/128");
        }

        [TearDown]
        public void CleanupForwardedHeaders()
        {
            configuration?.Dispose();
        }

        [Test]
        public async Task Headers_should_be_applied_when_caller_matches_network_but_not_proxy()
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

            // Verify configuration shows both proxies and networks
            Assert.That(requestInfo.Configuration.KnownProxies, Does.Contain("192.168.1.100"));
            Assert.That(requestInfo.Configuration.KnownNetworks, Does.Contain("127.0.0.0/8").Or.Contain("::1/128"));
        }

        class Context : ScenarioContext
        {
        }
    }
}
