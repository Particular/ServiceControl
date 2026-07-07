namespace ServiceControl.AcceptanceTests.Security.OpenIdConnect;

using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.OpenIdConnect;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;
using ServiceControl.Infrastructure.Auth;

/// <summary>
/// my/routes returns the API routes the current token may call, as { method, url_template } entries.
/// It is the per-instance authorization contract ServicePulse consumes: it gates UI on routes it
/// already calls rather than on the server's internal permission vocabulary.
/// </summary>
class When_my_routes_are_requested : AcceptanceTest
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
            .WithRoleBasedAuthorizationEnabled()
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
                response = await OpenIdConnectAssertions.SendRequestWithoutAuth(
                    HttpClient, HttpMethod.Get, "/api/my/routes");
                return response != null;
            })
            .Run();

        OpenIdConnectAssertions.AssertUnauthorized(response);
    }

    class Context : ScenarioContext;
}
