namespace ServiceControl.AcceptanceTests.Security.Authorization
{
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.OpenIdConnect;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// Acceptance tests for the <c>GET /api/me/permissions</c> descriptor endpoint (spec §5.4).
    /// The endpoint returns the calling user's effective permission set so ServicePulse can
    /// decide which UI controls to enable.
    /// </summary>
    class When_requesting_my_permissions : AcceptanceTest
    {
        OpenIdConnectTestConfiguration configuration;
        MockOidcServer mockOidcServer;

        const string TestAudience = "api://test-audience";
        const string TestClientId = "test-client-id";
        const string TestApiScopes = "api://test-audience/.default";

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
        public async Task Returns_the_effective_permission_set_for_operator_role()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    // Issue a token carrying the sc-operator realm role
                    var token = mockOidcServer.GenerateTokenWithRealmRoles(
                        subject: "alice",
                        realmRoles: ["sc-operator"]);

                    response = await OpenIdConnectAssertions.SendRequestWithBearerToken(
                        HttpClient,
                        HttpMethod.Get,
                        "/api/me/permissions",
                        token);

                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Authenticated operator should receive 200");

            var body = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // descriptor must include version, user, and permissions array
            Assert.That(root.TryGetProperty("version", out _), Is.True,
                "Response must contain 'version' field");
            Assert.That(root.TryGetProperty("user", out var userProp), Is.True,
                "Response must contain 'user' field");
            Assert.That(userProp.GetString(), Is.EqualTo("alice"));

            Assert.That(root.TryGetProperty("permissions", out var permsProp), Is.True,
                "Response must contain 'permissions' array");
            Assert.That(permsProp.ValueKind, Is.EqualTo(JsonValueKind.Array));

            // sc-operator should have messages:retry per the default rbac.yaml
            var hasRetry = false;
            foreach (var perm in permsProp.EnumerateArray())
            {
                if (perm.TryGetProperty("permission", out var p) &&
                    p.GetString() == "messages:retry")
                {
                    hasRetry = true;
                    break;
                }
            }
            Assert.That(hasRetry, Is.True,
                "sc-operator effective permissions must include messages:retry");
        }

        [Test]
        public async Task Unauthenticated_request_receives_401()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    response = await OpenIdConnectAssertions.SendRequestWithoutAuth(
                        HttpClient,
                        HttpMethod.Get,
                        "/api/me/permissions");

                    return response != null;
                })
                .Run();

            OpenIdConnectAssertions.AssertUnauthorized(response);
        }

        class Context : ScenarioContext;
    }
}
