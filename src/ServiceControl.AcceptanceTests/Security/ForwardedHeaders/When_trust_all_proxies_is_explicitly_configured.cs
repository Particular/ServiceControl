namespace ServiceControl.AcceptanceTests.Security.ForwardedHeaders
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.ForwardedHeaders;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// Explicit TrustAllProxies Configuration
    /// When TrustAllProxies is explicitly configured via environment variable, headers should be applied.
    /// This test verifies the WithTrustAllProxies() configuration method works correctly.
    /// </summary>
    class When_trust_all_proxies_is_explicitly_configured : AcceptanceTest
    {
        ForwardedHeadersTestConfiguration configuration;

        [SetUp]
        public void ConfigureForwardedHeaders() =>
            configuration = new ForwardedHeadersTestConfiguration(ServiceControlInstanceType.Primary)
                .WithTrustAllProxies();

        [TearDown]
        public void CleanupForwardedHeaders() => configuration?.Dispose();

        [Test]
        public async Task Headers_should_be_applied_when_trust_all_proxies_is_explicitly_set()
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

            ForwardedHeadersAssertions.AssertHeadersAppliedWhenTrustAllProxies(
                requestInfo,
                expectedScheme: "https",
                expectedHost: "example.com",
                expectedRemoteIp: "203.0.113.50");

            // Verify the configuration explicitly shows TrustAllProxies is true
            Assert.That(requestInfo.Configuration.TrustAllProxies, Is.True,
                "TrustAllProxies should be explicitly set to true");
        }

        class Context : ScenarioContext
        {
        }
    }
}
