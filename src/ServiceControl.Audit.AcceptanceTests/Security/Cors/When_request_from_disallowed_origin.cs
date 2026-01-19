namespace ServiceControl.Audit.AcceptanceTests.Security.Cors
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Cors;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// Request from Disallowed Origin
    /// When a request comes from an origin that is NOT in the AllowedOrigins list,
    /// the response should NOT include an Access-Control-Allow-Origin header that matches the request origin.
    /// </summary>
    class When_request_from_disallowed_origin : AcceptanceTest
    {
        CorsTestConfiguration configuration;

        [SetUp]
        public void ConfigureCors() =>
            // Configure specific allowed origins - the test origin won't be in this list
            configuration = new CorsTestConfiguration(ServiceControlInstanceType.Audit)
                .WithAllowedOrigins("https://app.example.com,https://admin.example.com");

        [TearDown]
        public void CleanupCors() => configuration?.Dispose();

        [Test]
        public async Task Should_not_return_access_control_allow_origin_header_for_disallowed_origin()
        {
            HttpResponseMessage response = null;
            const string disallowedOrigin = "https://malicious.example.com";

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    response = await CorsAssertions.SendRequestWithOrigin(
                        HttpClient,
                        origin: disallowedOrigin,
                        endpoint: "/api");
                    return response != null;
                })
                .Run();

            CorsAssertions.AssertOriginNotAllowed(response, disallowedOrigin);
        }

        [Test]
        public async Task Should_not_allow_origin_with_different_scheme()
        {
            HttpResponseMessage response = null;
            // http:// instead of https://
            const string disallowedOrigin = "http://app.example.com";

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    response = await CorsAssertions.SendRequestWithOrigin(
                        HttpClient,
                        origin: disallowedOrigin,
                        endpoint: "/api");
                    return response != null;
                })
                .Run();

            CorsAssertions.AssertOriginNotAllowed(response, disallowedOrigin);
        }

        [Test]
        public async Task Should_not_allow_origin_with_different_port()
        {
            HttpResponseMessage response = null;
            // Different port
            const string disallowedOrigin = "https://app.example.com:8080";

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    response = await CorsAssertions.SendRequestWithOrigin(
                        HttpClient,
                        origin: disallowedOrigin,
                        endpoint: "/api");
                    return response != null;
                })
                .Run();

            CorsAssertions.AssertOriginNotAllowed(response, disallowedOrigin);
        }

        [Test]
        public async Task Should_not_allow_subdomain_when_parent_domain_is_configured()
        {
            HttpResponseMessage response = null;
            // Subdomain of allowed origin
            const string disallowedOrigin = "https://sub.app.example.com";

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    response = await CorsAssertions.SendRequestWithOrigin(
                        HttpClient,
                        origin: disallowedOrigin,
                        endpoint: "/api");
                    return response != null;
                })
                .Run();

            CorsAssertions.AssertOriginNotAllowed(response, disallowedOrigin);
        }

        [Test]
        public async Task Preflight_request_should_not_allow_disallowed_origin()
        {
            HttpResponseMessage response = null;
            const string disallowedOrigin = "https://malicious.example.com";

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    response = await CorsAssertions.SendPreflightRequest(
                        HttpClient,
                        origin: disallowedOrigin,
                        requestMethod: "POST");
                    return response != null;
                })
                .Run();

            CorsAssertions.AssertOriginNotAllowed(response, disallowedOrigin);
        }

        class Context : ScenarioContext
        {
        }
    }
}
