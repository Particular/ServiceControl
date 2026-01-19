namespace ServiceControl.Audit.AcceptanceTests.Security.Cors
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Cors;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// CORS Disabled
    /// When AllowAnyOrigin is false and no AllowedOrigins are configured, CORS is effectively disabled.
    /// No Access-Control-Allow-Origin header should be returned for any origin.
    /// </summary>
    class When_cors_is_disabled : AcceptanceTest
    {
        CorsTestConfiguration configuration;

        [SetUp]
        public void ConfigureCors() =>
            // Disable CORS by setting AllowAnyOrigin to false and not configuring any origins
            configuration = new CorsTestConfiguration(ServiceControlInstanceType.Audit)
                .WithCorsDisabled();

        [TearDown]
        public void CleanupCors() => configuration?.Dispose();

        [Test]
        public async Task Should_not_return_access_control_allow_origin_header()
        {
            HttpResponseMessage response = null;
            const string testOrigin = "https://app.example.com";

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    response = await CorsAssertions.SendRequestWithOrigin(
                        HttpClient,
                        origin: testOrigin,
                        endpoint: "/api");
                    return response != null;
                })
                .Run();

            CorsAssertions.AssertCorsDisabled(response);
        }

        [Test]
        public async Task Preflight_request_should_not_return_cors_headers()
        {
            HttpResponseMessage response = null;
            const string testOrigin = "https://app.example.com";

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    response = await CorsAssertions.SendPreflightRequest(
                        HttpClient,
                        origin: testOrigin,
                        requestMethod: "POST");
                    return response != null;
                })
                .Run();

            CorsAssertions.AssertCorsDisabled(response);
        }

        class Context : ScenarioContext
        {
        }
    }
}
