namespace ServiceControl.AcceptanceTests.Security.OpenIdConnect
{
    using System.Net.Http;
    using System.Security.Claims;
    using System.Text.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.OpenIdConnect;
    using Infrastructure.Auth;
    using Infrastructure.WebApi;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// my/permissions and my/permissions/all
    /// These endpoints let a client (e.g. ServicePulse) discover what the current user is allowed to
    /// do: the full granular permission list, and a simplified per-area summary used to gate UI
    /// sections. Both are governed by the caller's role claims via <see cref="RolePermissions"/>.
    /// </summary>
    class When_my_permissions_are_requested : AcceptanceTest
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
                        HttpClient,
                        HttpMethod.Get,
                        "/api/my/permissions/all");
                    return response != null;
                })
                .Run();

            OpenIdConnectAssertions.AssertUnauthorized(response);
        }

        [Test]
        public async Task Should_return_only_the_permissions_granted_to_the_reader_role()
        {
            var descriptor = await GetPermissions(RolePermissions.Reader);

            Assert.That(descriptor.Permissions, Is.EquivalentTo(RolePermissions.GetPermissions(RolePermissions.Reader)));
        }

        [Test]
        public async Task Should_return_every_known_permission_for_the_writer_role()
        {
            var descriptor = await GetPermissions(RolePermissions.Writer);

            Assert.That(descriptor.Permissions, Is.EquivalentTo(Permissions.All));
        }

        [Test]
        public async Task Should_summarize_the_reader_role_as_read_only()
        {
            var summary = await GetSummary(RolePermissions.Reader);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(summary.FailedMessagesRead, Is.True);
                Assert.That(summary.FailedMessagesWrite, Is.False);
                Assert.That(summary.AuditingRead, Is.True);
                Assert.That(summary.MonitoringRead, Is.True);
                Assert.That(summary.MonitoringWrite, Is.False);
                Assert.That(summary.AdminRead, Is.True);
                Assert.That(summary.AdminWrite, Is.False);
            }
        }

        [Test]
        public async Task Should_summarize_the_writer_role_as_full_access()
        {
            var summary = await GetSummary(RolePermissions.Writer);

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

        [Test]
        public async Task Should_summarize_a_role_with_no_grants_as_all_false()
        {
            // A role that exists on the token but isn't recognised by RolePermissions grants nothing.
            var summary = await GetSummary("not-a-real-role");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(summary.FailedMessagesRead, Is.False);
                Assert.That(summary.FailedMessagesWrite, Is.False);
                Assert.That(summary.AuditingRead, Is.False);
                Assert.That(summary.MonitoringRead, Is.False);
                Assert.That(summary.MonitoringWrite, Is.False);
                Assert.That(summary.AdminRead, Is.False);
                Assert.That(summary.AdminWrite, Is.False);
            }
        }

        async Task<PermissionsResponse> GetPermissions(string role) =>
            await Get<PermissionsResponse>("/api/my/permissions/all", role);

        async Task<MeController.PermissionsSummary> GetSummary(string role) =>
            await Get<MeController.PermissionsSummary>("/api/my/permissions", role);

        async Task<T> Get<T>(string path, string role)
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateToken(additionalClaims: [new Claim("roles", role)]);
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