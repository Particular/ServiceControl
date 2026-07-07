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

    [Test]
    public async Task Should_return_only_the_routes_the_callers_role_permits()
    {
        HttpResponseMessage response = null;

        _ = await Define<Context>()
            .Done(async ctx =>
            {
                // A reader holds every :view permission but none of the operate ones. The manifest is
                // the projection of exactly what the server enforces, so it must advertise a view route
                // the reader may call and omit a retry route it may not.
                var readerToken = mockOidcServer.GenerateToken(
                    additionalClaims: new[] { new Claim("roles", "reader") });
                response = await OpenIdConnectAssertions.SendRequestWithBearerToken(
                    HttpClient, HttpMethod.Get, "/api/my/routes", readerToken);
                return response != null;
            })
            .Run();

        OpenIdConnectAssertions.AssertAuthenticated(response);

        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        var roles = root.GetProperty("roles").EnumerateArray().Select(role => role.GetString());
        Assert.That(roles, Does.Contain("reader"));

        var routes = root.GetProperty("routes").EnumerateArray()
            .Select(entry => $"{entry.GetProperty("method").GetString()} {entry.GetProperty("url_template").GetString()}")
            .ToArray();

        Assert.That(routes, Does.Contain("GET /api/errors"),
            "Reader holds error:messages:view, so the errors list route must be advertised.");
        Assert.That(routes, Does.Not.Contain("POST /api/errors/retry/all"),
            "Reader lacks error:messages:retry, so the retry route must be filtered out.");
    }

    class Context : ScenarioContext;
}
