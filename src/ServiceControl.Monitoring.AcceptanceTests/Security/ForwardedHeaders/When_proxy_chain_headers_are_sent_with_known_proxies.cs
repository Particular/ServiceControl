namespace ServiceControl.Monitoring.AcceptanceTests.Security.ForwardedHeaders
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.ForwardedHeaders;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// Tests Scenario 9: Proxy Chain with Known Proxies (ForwardLimit=1) from local-forward-headers-testing.md
    /// When TrustAllProxies=false (known proxies configured), ForwardLimit=1, so only the last proxy IP is processed.
    /// </summary>
    class When_proxy_chain_headers_are_sent_with_known_proxies : AcceptanceTest
    {
        ForwardedHeadersTestConfiguration configuration;

        [SetUp]
        public void ConfigureForwardedHeaders()
        {
            // Configure known proxies to include localhost (test server uses localhost)
            configuration = new ForwardedHeadersTestConfiguration(ServiceControlInstanceType.Monitoring)
                .WithKnownProxies("127.0.0.1,::1");
        }

        [TearDown]
        public void CleanupForwardedHeaders()
        {
            configuration?.Dispose();
        }

        [Test]
        public async Task Only_last_proxy_ip_should_be_processed_when_forward_limit_is_one()
        {
            RequestInfoResponse requestInfo = null;

            await Define<Context>()
                .Done(async ctx =>
                {
                    // Simulate a proxy chain: client -> proxy1 -> proxy2 -> ServiceControl.Monitoring
                    // X-Forwarded-For: 203.0.113.50, 10.0.0.1, 192.168.1.1
                    // Expected with ForwardLimit=1: 192.168.1.1 (last proxy in chain)
                    requestInfo = await ForwardedHeadersAssertions.GetRequestInfo(
                        HttpClient,
                        SerializerOptions,
                        xForwardedFor: "203.0.113.50, 10.0.0.1, 192.168.1.1",
                        xForwardedProto: "https",
                        xForwardedHost: "example.com");
                    return requestInfo != null;
                })
                .Run();

            ForwardedHeadersAssertions.AssertProxyChainWithForwardLimitOne(
                requestInfo,
                expectedLastProxyIp: "192.168.1.1",
                expectedScheme: "https",
                expectedHost: "example.com",
                expectedRemainingForwardedFor: "203.0.113.50,10.0.0.1");
        }

        class Context : ScenarioContext
        {
        }
    }
}
