namespace ServiceControl.AcceptanceTests.Security.OpenIdConnect
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.OpenIdConnect;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// Authentication enabled, role-based authorization disabled (the default). Every route that is not
    /// [AllowAnonymous] must still require a valid token. Roles are not checked, so a valid token without
    /// roles is accepted.
    /// </summary>
    [NonParallelizable]
    class When_authentication_is_enabled_without_role_based_authorization : AcceptanceTest
    {
        OpenIdConnectTestConfiguration configuration;
        MockOidcServer mockOidcServer;

        const string TestAudience = "api://test-audience";

        [SetUp]
        public void ConfigureAuth()
        {
            mockOidcServer = new MockOidcServer(audience: TestAudience);
            mockOidcServer.Start();

            configuration = new OpenIdConnectTestConfiguration(ServiceControlInstanceType.Primary)
                .WithConfigurationValidationDisabled()
                .WithAuthenticationEnabled()
                .WithAuthority(mockOidcServer.Authority)
                .WithAudience(TestAudience)
                .WithServicePulseClientId("test-client-id")
                .WithServicePulseApiScopes("[\"api://test-audience/.default\"]")
                .WithRequireHttpsMetadata(false);
        }

        [TearDown]
        public void CleanupAuth()
        {
            configuration?.Dispose();
            mockOidcServer?.Dispose();
        }

        [Test]
        public async Task Should_reject_anonymous_requests_on_every_protected_route()
        {
            IReadOnlyList<string> notRejected = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    notRejected = await ProtectedRoutes.FindRoutesNotRejectingAnonymousRequests(
                        HttpClient,
                        ServiceProvider.GetRequiredService<EndpointDataSource>());
                    return true;
                })
                .Run();

            Assert.That(notRejected, Is.Empty, "Protected routes that did not answer 401 to a request without a token");
        }

        [Test]
        public async Task Should_accept_token_without_roles()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var tokenWithoutRoles = mockOidcServer.GenerateToken();
                    response = await OpenIdConnectAssertions.SendRequestWithBearerToken(
                        HttpClient,
                        HttpMethod.Get,
                        "/api/errors",
                        tokenWithoutRoles);
                    return response != null;
                })
                .Run();

            OpenIdConnectAssertions.AssertAuthenticated(response);
        }

        class Context : ScenarioContext;
    }
}
