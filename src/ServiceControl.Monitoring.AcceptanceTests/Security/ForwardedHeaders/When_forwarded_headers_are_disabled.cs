namespace ServiceControl.Monitoring.AcceptanceTests.Security.ForwardedHeaders
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.ForwardedHeaders;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// When forwarded headers processing is disabled, headers should be ignored regardless of trust.
    /// </summary>
    class When_forwarded_headers_are_disabled : AcceptanceTest
    {
        ForwardedHeadersTestConfiguration configuration;

        [SetUp]
        public void ConfigureForwardedHeaders() =>
            // Disable forwarded headers processing entirely
            configuration = new ForwardedHeadersTestConfiguration(ServiceControlInstanceType.Monitoring)
                .WithForwardedHeadersDisabled();

        [TearDown]
        public void CleanupForwardedHeaders() => configuration?.Dispose();

        [Test]
        public async Task Headers_should_be_ignored_when_disabled()
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

            ForwardedHeadersAssertions.AssertHeadersIgnoredWhenDisabled(
                requestInfo,
                sentXForwardedFor: "203.0.113.50",
                sentXForwardedProto: "https",
                sentXForwardedHost: "example.com");
        }

        class Context : ScenarioContext
        {
        }
    }
}
