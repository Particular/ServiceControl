namespace ServiceControl.AcceptanceTests.Security.OpenIdConnect
{
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.OpenIdConnect;
    using Infrastructure.Auth;
    using Infrastructure.WebApi;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// When the caller is authenticated but Authentication.RoleBasedAuthorizationEnabled is false (the
    /// default), every permission is implicitly granted (mirrors PermissionPolicyProvider's allow-all
    /// policy). my/permissions and my/permissions/all should reflect that even for a token that carries
    /// no "roles" claim at all.
    /// </summary>
    class When_role_based_authorization_is_disabled : AcceptanceTest
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
                // Role-based authorization deliberately left disabled (the default).
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
        public async Task Should_grant_every_known_permission_regardless_of_roles()
        {
            var descriptor = await Get<MeController.PermissionsDescriptor>("/api/my/permissions/all");

            Assert.That(descriptor.Permissions, Is.EquivalentTo(Permissions.All));
        }

        [Test]
        public async Task Should_summarize_as_full_access_regardless_of_roles()
        {
            var summary = await Get<MeController.PermissionsSummary>("/api/my/permissions");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(summary.FailedMessagesRead, Is.True);
                Assert.That(summary.FailedMessagesWrite, Is.True);
                Assert.That(summary.AuditingRead, Is.True);
                Assert.That(summary.MonitoringRead, Is.True);
                Assert.That(summary.MonitoringWrite, Is.True);
                Assert.That(summary.AdminRead, Is.True);
                Assert.That(summary.AdminWrite, Is.True);
            }
        }

        async Task<T> Get<T>(string path)
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    // No "roles" claim at all - allow-all must not depend on the token carrying any role.
                    var token = mockOidcServer.GenerateToken();
                    response = await OpenIdConnectAssertions.SendRequestWithBearerToken(
                        HttpClient,
                        HttpMethod.Get,
                        path,
                        token);
                    return response != null;
                })
                .Run();

            OpenIdConnectAssertions.AssertAuthenticated(response);

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(content, SerializerOptions);
        }

        class Context : ScenarioContext;
    }
}