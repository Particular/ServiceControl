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
/// my/routes returns the API routes the current token may call, as { method, urlTemplate } entries.
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

    [Test]
    public async Task Reader_can_view_but_cannot_retry()
    {
        var routes = await GetRoutes(RolePermissions.Reader);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(routes.Any(r => r.UrlTemplate == "/api/configuration"), Is.True,
                "reader holds :view permissions, so view routes are allowed");
            Assert.That(routes.Any(r => r.Method == "POST" && r.UrlTemplate.EndsWith("/retry")), Is.False,
                "reader has no retry permission, so retry routes are excluded");
        }
    }

    [Test]
    public async Task Writer_can_retry()
    {
        var routes = await GetRoutes(RolePermissions.Writer);

        Assert.That(routes.Any(r => r.Method == "POST" && r.UrlTemplate.EndsWith("/retry")), Is.True,
            "writer holds the operate permissions, so retry routes are allowed");
    }

    async Task<List<RouteManifestEntry>> GetRoutes(string role)
    {
        HttpResponseMessage response = null;

        _ = await Define<Context>()
            .Done(async ctx =>
            {
                var token = mockOidcServer.GenerateToken(additionalClaims: [new Claim("roles", role)]);
                response = await OpenIdConnectAssertions.SendRequestWithBearerToken(
                    HttpClient, HttpMethod.Get, "/api/my/routes", token);
                return response != null;
            })
            .Run();

        OpenIdConnectAssertions.AssertAuthenticated(response);

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<RouteManifestEntry>>(content, SerializerOptions);
    }

    class Context : ScenarioContext;
}
