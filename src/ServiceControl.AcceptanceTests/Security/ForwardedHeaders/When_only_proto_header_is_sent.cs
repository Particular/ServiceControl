namespace ServiceControl.AcceptanceTests.Security.ForwardedHeaders
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.ForwardedHeaders;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// Partial Headers (Proto Only)
    /// When only X-Forwarded-Proto is sent, only scheme should change.
    /// </summary>
    class When_only_proto_header_is_sent : AcceptanceTest
    {
        [Test]
        public async Task Only_scheme_should_be_changed()
        {
            RequestInfoResponse requestInfo = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    requestInfo = await ForwardedHeadersAssertions.GetRequestInfo(
                        HttpClient,
                        SerializerOptions,
                        xForwardedProto: "https");
                    return requestInfo != null;
                })
                .Run();

            ForwardedHeadersAssertions.AssertPartialHeadersApplied(requestInfo, expectedScheme: "https");
        }

        class Context : ScenarioContext
        {
        }
    }
}
