namespace ServiceControl.AcceptanceTests.Security.Cors
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Cors;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// Request from Allowed Origin
    /// When a request comes from an origin that is in the AllowedOrigins list,
    /// the response should include the Access-Control-Allow-Origin header with that specific origin.
    /// </summary>
    class When_request_from_allowed_origin : AcceptanceTest
    {
        CorsTestConfiguration configuration;

        [SetUp]
        public void ConfigureCors() =>
            // Configure specific allowed origins
            configuration = new CorsTestConfiguration(ServiceControlInstanceType.Primary)
                .WithAllowedOrigins("https://app.example.com,https://admin.example.com");

        [TearDown]
        public void CleanupCors() => configuration?.Dispose();

        [TestCase("https://app.example.com")]
        [TestCase("https://admin.example.com")]
        public async Task Should_return_matching_origin_in_access_control_allow_origin_header(string allowedOrigin)
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    response = await CorsAssertions.SendRequestWithOrigin(
                        HttpClient,
                        origin: allowedOrigin,
                        endpoint: "/api");
                    return response != null;
                })
                .Run();

            CorsAssertions.AssertAllowedOrigin(response, allowedOrigin);
        }

        [Test]
        public async Task Preflight_request_should_return_correct_cors_headers()
        {
            HttpResponseMessage response = null;
            const string allowedOrigin = "https://app.example.com";

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    response = await CorsAssertions.SendPreflightRequest(
                        HttpClient,
                        origin: allowedOrigin,
                        requestMethod: "POST");
                    return response != null;
                })
                .Run();

            CorsAssertions.AssertAllowedOrigin(response, allowedOrigin);
            CorsAssertions.AssertAllowedMethods(response, "POST", "GET", "PUT", "DELETE", "OPTIONS", "PATCH", "HEAD");
        }

        class Context : ScenarioContext
        {
        }
    }
}
