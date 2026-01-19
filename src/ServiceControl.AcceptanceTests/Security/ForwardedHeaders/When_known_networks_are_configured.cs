namespace ServiceControl.AcceptanceTests.Security.ForwardedHeaders
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.ForwardedHeaders;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// Known Networks (CIDR)
    /// When KnownNetworks are configured and the caller IP falls within, headers should be applied.
    /// </summary>
    class When_known_networks_are_configured : AcceptanceTest
    {
        ForwardedHeadersTestConfiguration configuration;

        [SetUp]
        public void ConfigureForwardedHeaders() =>
            // Configure known networks to include localhost CIDR ranges (test server uses localhost)
            configuration = new ForwardedHeadersTestConfiguration(ServiceControlInstanceType.Primary)
                .WithKnownNetworks("127.0.0.0/8,::1/128");

        [TearDown]
        public void CleanupForwardedHeaders() => configuration?.Dispose();

        [Test]
        public async Task Headers_should_be_applied_when_caller_matches_known_network()
        {
            RequestInfoResponse requestInfo = null;

            _ = await Define<Context>()
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

            // Verify configuration shows known networks
            Assert.That(requestInfo.Configuration.KnownNetworks, Does.Contain("127.0.0.0/8").Or.Contain("::1/128"));
        }

        class Context : ScenarioContext
        {
        }
    }
}
