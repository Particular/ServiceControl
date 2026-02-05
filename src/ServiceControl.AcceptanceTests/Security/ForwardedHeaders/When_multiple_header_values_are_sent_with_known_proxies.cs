namespace ServiceControl.AcceptanceTests.Security.ForwardedHeaders
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.ForwardedHeaders;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// Multiple Header Values with Known Proxies (ForwardLimit=1)
    /// When TrustAllProxies=false (known proxies configured), ForwardLimit=1,
    /// so only the rightmost values are processed.
    /// </summary>
    class When_multiple_header_values_are_sent_with_known_proxies : AcceptanceTest
    {
        ForwardedHeadersTestConfiguration configuration;

        [SetUp]
        public void ConfigureForwardedHeaders() =>
            // Configure known proxies to include localhost (test server uses localhost)
            configuration = new ForwardedHeadersTestConfiguration(ServiceControlInstanceType.Primary)
                .WithKnownProxies("127.0.0.1,::1");

        [TearDown]
        public void CleanupForwardedHeaders() => configuration?.Dispose();

        [Test]
        public async Task Only_rightmost_values_should_be_processed_when_forward_limit_is_one()
        {
            RequestInfoResponse requestInfo = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    // Simulate multiple proxy values in all headers
                    // X-Forwarded-Proto: https, http
                    // X-Forwarded-Host: example.com, internal.proxy.local
                    // X-Forwarded-For: 203.0.113.50, 10.0.0.1
                    // Expected with ForwardLimit=1: http, internal.proxy.local, 10.0.0.1 (rightmost values)
                    requestInfo = await ForwardedHeadersAssertions.GetRequestInfo(
                        HttpClient,
                        SerializerOptions,
                        xForwardedFor: "203.0.113.50, 10.0.0.1",
                        xForwardedProto: "https, http",
                        xForwardedHost: "example.com, internal.proxy.local");
                    return requestInfo != null;
                })
                .Run();

            ForwardedHeadersAssertions.AssertMultipleHeaderValuesWithForwardLimitOne(
                requestInfo,
                expectedLastScheme: "http",
                expectedLastHost: "internal.proxy.local",
                expectedLastProxyIp: "10.0.0.1",
                expectedRemainingForwardedFor: "203.0.113.50",
                expectedRemainingForwardedProto: "https",
                expectedRemainingForwardedHost: "example.com");
        }

        class Context : ScenarioContext
        {
        }
    }
}
