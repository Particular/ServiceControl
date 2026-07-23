namespace ServiceControl.AcceptanceTests.Security.OpenIdConnect
{
    using System.Net.Http;
    using System.Security.Claims;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.OpenIdConnect;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// Authentication Enabled
    /// When Authentication.Enabled is true, the authentication configuration endpoint should
    /// return the configured OIDC settings so that clients (like ServicePulse) can discover
    /// how to authenticate.
    ///
    /// This test uses a mock OIDC server to provide discovery endpoints and signing keys,
    /// allowing full testing of the JWT Bearer authentication flow.
    /// </summary>
    class When_authentication_is_enabled : AcceptanceTest
    {
        OpenIdConnectTestConfiguration configuration;
        MockOidcServer mockOidcServer;

        const string TestAudience = "api://test-audience";
        const string TestClientId = "test-client-id";
        const string TestApiScopes = "api://test-audience/.default";

        [SetUp]
        public void ConfigureAuth()
        {
            // Start mock OIDC server
            mockOidcServer = new MockOidcServer(audience: TestAudience);
            mockOidcServer.Start();

            configuration = new OpenIdConnectTestConfiguration(ServiceControlInstanceType.Primary)
                .WithConfigurationValidationDisabled()
                .WithAuthenticationEnabled()
                .WithRoleBasedAuthorizationEnabled()
                .WithAuthority(mockOidcServer.Authority)
                .WithAudience(TestAudience)
                .WithServicePulseClientId(TestClientId)
                .WithServicePulseApiScopes(TestApiScopes)
                .WithRequireHttpsMetadata(false);
        }

        [TearDown]
        public void CleanupAuth()
        {
            configuration?.Dispose();
            mockOidcServer?.Dispose();
        }

        [Test]
        public async Task Should_return_authentication_configuration_with_enabled_true()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    // The authentication/configuration endpoint is marked [AllowAnonymous]
                    // so it should be accessible without authentication
                    response = await OpenIdConnectAssertions.SendRequestWithoutAuth(
                        HttpClient,
                        HttpMethod.Get,
                        "/api/authentication/configuration");
                    return response != null;
                })
                .Run();

            await OpenIdConnectAssertions.AssertAuthConfigurationResponse(
                response,
                expectedEnabled: true,
                expectedClientId: TestClientId,
                expectedAudience: TestAudience,
                expectedApiScopes: TestApiScopes,
                expectedScopes: $"{TestApiScopes} openid profile email offline_access",
                expectedRoleBasedAuthorizationEnabled: true);
        }

        [Test]
        public async Task Should_reject_requests_without_bearer_token()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    // Use /api/errors which does NOT have [AllowAnonymous] so it should require authentication
                    // Note: /api is marked [AllowAnonymous] for server-to-server configuration fetching
                    response = await OpenIdConnectAssertions.SendRequestWithoutAuth(
                        HttpClient,
                        HttpMethod.Get,
                        "/api/errors");
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
                        "/api/errors",
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
                    // The "reader" role grants every :view permission, including error:messages:view
                    // required by /api/errors. Without a role-bearing claim the request would be 403.
                    var validToken = mockOidcServer.GenerateToken(
                        additionalClaims: new[] { new Claim("roles", "reader") });
                    response = await OpenIdConnectAssertions.SendRequestWithBearerToken(
                        HttpClient,
                        HttpMethod.Get,
                        "/api/errors",
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
                        "/api/errors",
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
                        "/api/errors",
                        wrongAudienceToken);
                    return response != null;
                })
                .Run();

            OpenIdConnectAssertions.AssertUnauthorized(response);
        }

        [Test]
        public async Task Should_reject_requests_with_wrong_issuer()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var wrongIssuerToken = mockOidcServer.GenerateTokenWithWrongIssuer();
                    response = await OpenIdConnectAssertions.SendRequestWithBearerToken(
                        HttpClient,
                        HttpMethod.Get,
                        "/api/errors",
                        wrongIssuerToken);
                    return response != null;
                })
                .Run();

            OpenIdConnectAssertions.AssertUnauthorized(response);
        }

        [Test]
        public async Task Should_forbid_authenticated_user_lacking_required_permission()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    // The "reader" role grants every :view permission but none of the operate ones.
                    // Retrying messages requires error:messages:retry (writer/admin only), so an
                    // authenticated reader must be forbidden (403), not merely unauthenticated (401).
                    var readerToken = mockOidcServer.GenerateToken(
                        additionalClaims: new[] { new Claim("roles", "reader") });
                    response = await OpenIdConnectAssertions.SendRequestWithBearerToken(
                        HttpClient,
                        HttpMethod.Post,
                        "/api/errors/retry/all",
                        readerToken);
                    return response != null;
                })
                .Run();

            OpenIdConnectAssertions.AssertForbidden(response);
        }

        class Context : ScenarioContext;
    }
}
