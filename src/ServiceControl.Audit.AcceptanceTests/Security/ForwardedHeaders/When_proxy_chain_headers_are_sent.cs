namespace ServiceControl.Audit.AcceptanceTests.Security.ForwardedHeaders
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.ForwardedHeaders;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// Tests Scenario 8: Proxy Chain (Multiple X-Forwarded-For Values) from local-forward-headers-testing.md
    /// When TrustAllProxies is true and X-Forwarded-For contains multiple IPs (proxy chain),
    /// the original client IP (first in the chain) should be returned.
    /// </summary>
    class When_proxy_chain_headers_are_sent : AcceptanceTest
    {
        [Test]
        public async Task Original_client_ip_should_be_returned_when_trust_all_proxies()
        {
            RequestInfoResponse requestInfo = null;

            await Define<Context>()
                .Done(async ctx =>
                {
                    // Simulate a proxy chain: client -> proxy1 -> proxy2 -> ServiceControl.Audit
                    // X-Forwarded-For: 203.0.113.50, 10.0.0.1, 192.168.1.1
                    // Expected: 203.0.113.50 (original client)
                    requestInfo = await ForwardedHeadersAssertions.GetRequestInfo(
                        HttpClient,
                        SerializerOptions,
                        xForwardedFor: "203.0.113.50, 10.0.0.1, 192.168.1.1",
                        xForwardedProto: "https",
                        xForwardedHost: "example.com");
                    return requestInfo != null;
                })
                .Run();

            ForwardedHeadersAssertions.AssertProxyChainProcessedWithTrustAllProxies(
                requestInfo,
                expectedOriginalClientIp: "203.0.113.50",
                expectedScheme: "https",
                expectedHost: "example.com");
        }

        class Context : ScenarioContext
        {
        }
    }
}
