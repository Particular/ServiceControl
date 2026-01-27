namespace ServiceControl.Audit.AcceptanceTests.Security.ForwardedHeaders
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.ForwardedHeaders;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// Default Behavior with Headers
    /// When forwarded headers are sent and TrustAllProxies is true (default), headers should be applied.
    /// </summary>
    class When_forwarded_headers_are_sent : AcceptanceTest
    {
        [Test]
        public async Task Headers_should_be_applied_when_trust_all_proxies()
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
        }

        class Context : ScenarioContext
        {
        }
    }
}
