namespace ServiceControl.Audit.AcceptanceTests.Security.ForwardedHeaders
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.ForwardedHeaders;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// Tests Scenario 0: Direct Access (No Proxy) from local-forward-headers-testing.md
    /// When no forwarded headers are sent, the request values should remain unchanged.
    /// </summary>
    class When_request_has_no_forwarded_headers : AcceptanceTest
    {
        [Test]
        public async Task Request_values_should_remain_unchanged()
        {
            RequestInfoResponse requestInfo = null;

            await Define<Context>()
                .Done(async ctx =>
                {
                    var result = await this.TryGet<RequestInfoResponse>("/debug/request-info");
                    if (result.HasResult)
                    {
                        requestInfo = result.Item;
                        return true;
                    }
                    return false;
                })
                .Run();

            ForwardedHeadersAssertions.AssertDirectAccessWithNoForwardedHeaders(requestInfo);
        }

        class Context : ScenarioContext
        {
        }
    }
}
