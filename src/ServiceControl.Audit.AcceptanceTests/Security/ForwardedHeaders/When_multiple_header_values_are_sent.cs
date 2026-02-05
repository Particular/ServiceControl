namespace ServiceControl.Audit.AcceptanceTests.Security.ForwardedHeaders
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.ForwardedHeaders;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// Multiple X-Forwarded-Proto and X-Forwarded-Host Values
    /// When TrustAllProxies is true and headers contain multiple values (proxy chain),
    /// the original (leftmost) values should be returned.
    /// </summary>
    class When_multiple_header_values_are_sent : AcceptanceTest
    {
        [Test]
        public async Task Original_values_should_be_returned_when_trust_all_proxies()
        {
            RequestInfoResponse requestInfo = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    // Simulate multiple proxy values in all headers
                    // X-Forwarded-Proto: https, http (edge used https, internal proxy used http)
                    // X-Forwarded-Host: example.com, internal.proxy.local
                    // X-Forwarded-For: 203.0.113.50, 10.0.0.1
                    // Expected: https, example.com, 203.0.113.50 (original values)
                    requestInfo = await ForwardedHeadersAssertions.GetRequestInfo(
                        HttpClient,
                        SerializerOptions,
                        xForwardedFor: "203.0.113.50, 10.0.0.1",
                        xForwardedProto: "https, http",
                        xForwardedHost: "example.com, internal.proxy.local");
                    return requestInfo != null;
                })
                .Run();

            ForwardedHeadersAssertions.AssertMultipleHeaderValuesProcessedWithTrustAllProxies(
                requestInfo,
                expectedOriginalScheme: "https",
                expectedOriginalHost: "example.com",
                expectedOriginalClientIp: "203.0.113.50");
        }

        class Context : ScenarioContext
        {
        }
    }
}
