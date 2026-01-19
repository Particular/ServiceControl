namespace ServiceControl.Monitoring.AcceptanceTests.Security.OpenIdConnect
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.OpenIdConnect;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// Authentication Enabled
    /// When Authentication.Enabled is true, API endpoints should require a valid JWT Bearer token
    /// unless marked with [AllowAnonymous].
    ///
    /// This test uses a mock OIDC server to provide discovery endpoints and signing keys,
    /// allowing full testing of the JWT Bearer authentication flow.
    /// </summary>
    class When_authentication_is_enabled : AcceptanceTest
    {
        OpenIdConnectTestConfiguration configuration;
        MockOidcServer mockOidcServer;

        const string TestAudience = "api://test-audience";

        [SetUp]
        public void ConfigureAuth()
        {
            // Start mock OIDC server
            mockOidcServer = new MockOidcServer(audience: TestAudience);
            mockOidcServer.Start();

            configuration = new OpenIdConnectTestConfiguration(ServiceControlInstanceType.Monitoring)
                .WithConfigurationValidationDisabled()
                .WithAuthenticationEnabled()
                .WithAuthority(mockOidcServer.Authority)
                .WithAudience(TestAudience)
                .WithRequireHttpsMetadata(false);
        }

        [TearDown]
        public void CleanupAuth()
        {
            configuration?.Dispose();
            mockOidcServer?.Dispose();
        }

        [Test]
        public async Task Should_reject_requests_without_bearer_token()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    // Use /monitored-endpoints which does NOT have [AllowAnonymous] so it should require authentication
                    response = await OpenIdConnectAssertions.SendRequestWithoutAuth(
                        HttpClient,
                        HttpMethod.Get,
                        "/monitored-endpoints");
                    return response != null;
                })
                .Run();

            OpenIdConnectAssertions.AssertUnauthorized(response);
        }

        [Test]
        public async Task Should_reject_requests_with_invalid_bearer_token()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    response = await OpenIdConnectAssertions.SendRequestWithBearerToken(
                        HttpClient,
                        HttpMethod.Get,
                        "/monitored-endpoints",
                        "invalid-token-value");
                    return response != null;
                })
                .Run();

            OpenIdConnectAssertions.AssertUnauthorized(response);
        }

        [Test]
        public async Task Should_accept_requests_with_valid_bearer_token()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var validToken = mockOidcServer.GenerateToken();
                    response = await OpenIdConnectAssertions.SendRequestWithBearerToken(
                        HttpClient,
                        HttpMethod.Get,
                        "/monitored-endpoints",
                        validToken);
                    return response != null;
                })
                .Run();

            OpenIdConnectAssertions.AssertAuthenticated(response);
        }

        [Test]
        public async Task Should_reject_requests_with_expired_token()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var expiredToken = mockOidcServer.GenerateExpiredToken();
                    response = await OpenIdConnectAssertions.SendRequestWithBearerToken(
                        HttpClient,
                        HttpMethod.Get,
                        "/monitored-endpoints",
                        expiredToken);
                    return response != null;
                })
                .Run();

            OpenIdConnectAssertions.AssertUnauthorized(response);
        }

        [Test]
        public async Task Should_reject_requests_with_wrong_audience()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var wrongAudienceToken = mockOidcServer.GenerateTokenWithWrongAudience();
                    response = await OpenIdConnectAssertions.SendRequestWithBearerToken(
                        HttpClient,
                        HttpMethod.Get,
                        "/monitored-endpoints",
                        wrongAudienceToken);
                    return response != null;
                })
                .Run();

            OpenIdConnectAssertions.AssertUnauthorized(response);
        }

        [Test]
        public async Task Should_allow_anonymous_access_to_root_endpoint()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    // The root endpoint (/) is marked [AllowAnonymous] for discovery
                    response = await OpenIdConnectAssertions.SendRequestWithoutAuth(
                        HttpClient,
                        HttpMethod.Get,
                        "/");
                    return response != null;
                })
                .Run();

            OpenIdConnectAssertions.AssertNoAuthenticationRequired(response);
        }

        class Context : ScenarioContext
        {
        }
    }
}
