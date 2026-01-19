namespace ServiceControl.Monitoring.AcceptanceTests.Security.Cors
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Cors;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// Default CORS Behavior (AllowAnyOrigin = true)
    /// When AllowAnyOrigin is true (default for backwards compatibility), requests from any origin should be allowed.
    /// The Access-Control-Allow-Origin header should be "*".
    /// </summary>
    class When_cors_allows_any_origin : AcceptanceTest
    {
        CorsTestConfiguration configuration;

        [SetUp]
        public void ConfigureCors() =>
            // Default behavior - allow any origin
            configuration = new CorsTestConfiguration(ServiceControlInstanceType.Monitoring)
                .WithAllowAnyOrigin();

        [TearDown]
        public void CleanupCors() => configuration?.Dispose();

        [Test]
        public async Task Should_return_wildcard_access_control_allow_origin_header()
        {
            HttpResponseMessage response = null;
            const string testOrigin = "https://app.example.com";

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    response = await CorsAssertions.SendRequestWithOrigin(
                        HttpClient,
                        origin: testOrigin,
                        endpoint: "/");
                    return response != null;
                })
                .Run();

            CorsAssertions.AssertAllowAnyOrigin(response, testOrigin);
        }

        [Test]
        public async Task Should_return_expected_allowed_methods()
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

            CorsAssertions.AssertAllowedMethods(response, "POST", "GET", "PUT", "DELETE", "OPTIONS", "PATCH", "HEAD");
        }

        [Test]
        public async Task Should_return_expected_exposed_headers()
        {
            HttpResponseMessage response = null;
            const string testOrigin = "https://app.example.com";

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    response = await CorsAssertions.SendRequestWithOrigin(
                        HttpClient,
                        origin: testOrigin,
                        endpoint: "/");
                    return response != null;
                })
                .Run();

            CorsAssertions.AssertExposedHeaders(response, "ETag", "Last-Modified", "Link", "Total-Count", "X-Particular-Version");
        }

        class Context : ScenarioContext
        {
        }
    }
}
