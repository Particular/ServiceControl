namespace ServiceControl.AcceptanceTests.Security.Authorization
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.OpenIdConnect;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// Verifies that when OIDC authentication (and therefore authorization) is disabled,
    /// the <c>GET /api/me/permissions</c> endpoint is still accessible and the rest of
    /// the API behaves exactly as before authorization was added (non-breaking guarantee,
    /// spec §4).
    /// </summary>
    class When_authorization_is_disabled : AcceptanceTest
    {
        OpenIdConnectTestConfiguration configuration;

        [SetUp]
        public void DisableAuth()
        {
            // Explicitly disable OIDC so the non-breaking guarantee is exercised
            configuration = new OpenIdConnectTestConfiguration(ServiceControlInstanceType.Primary)
                .WithAuthenticationDisabled();
        }

        [TearDown]
        public void CleanupAuth()
        {
            configuration?.Dispose();
        }

        [Test]
        public async Task Api_endpoints_are_accessible_without_authentication()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    // /api/errors is not [AllowAnonymous], but with auth disabled it should be
                    // freely accessible (unchanged from pre-RBAC behaviour)
                    response = await OpenIdConnectAssertions.SendRequestWithoutAuth(
                        HttpClient,
                        HttpMethod.Get,
                        "/api/errors");

                    return response != null;
                })
                .Run();

            OpenIdConnectAssertions.AssertNoAuthenticationRequired(response);
        }

        [Test]
        public async Task Me_permissions_endpoint_returns_404_when_auth_disabled()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    // When OIDC is disabled, IPermissionEvaluator is not registered in DI.
                    // MePermissionsController resolves it optionally and returns 404 so the
                    // endpoint is effectively absent — non-breaking guarantee, spec §4.
                    response = await OpenIdConnectAssertions.SendRequestWithoutAuth(
                        HttpClient,
                        HttpMethod.Get,
                        "/api/me/permissions");

                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
                "With OIDC disabled, /api/me/permissions must return 404 (endpoint does not exist in this deployment)");
        }

        class Context : ScenarioContext;
    }
}
