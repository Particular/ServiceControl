namespace ServiceControl.AcceptanceTests.Security.Authorization
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Auth;
    using AcceptanceTesting.OpenIdConnect;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// S2 RBAC enforcement tests for the recoverability group endpoints:
    /// - GET  api/recoverability/groups/{classifier?}          (recoverabilitygroups:view)
    /// - GET  api/recoverability/groups/{id}/errors            (recoverabilitygroups:view, R1 paged)
    /// - HEAD api/recoverability/groups/{id}/errors            (recoverabilitygroups:view)
    /// - GET  api/recoverability/groups/id/{id}               (recoverabilitygroups:view)
    /// - GET  api/recoverability/history                       (recoverabilitygroups:view)
    /// - GET  api/recoverability/classifiers                   (recoverabilitygroups:view)
    /// - POST api/recoverability/groups/{id}/comment           (recoverabilitygroups:view)
    /// - DELETE api/recoverability/groups/{id}/comment         (recoverabilitygroups:view)
    /// - POST api/recoverability/groups/{id}/errors/retry      (recoverabilitygroups:retry)
    /// - POST api/recoverability/groups/{id}/errors/archive    (recoverabilitygroups:archive)
    /// - POST api/recoverability/groups/{id}/errors/unarchive  (recoverabilitygroups:unarchive)
    ///
    /// Special: group write operations fail-closed for scoped users (v1 limitation).
    /// </summary>
    class When_operating_on_recoverability_groups_s2 : AcceptanceTest
    {
        const string TestAudience = "api://test-audience";
        const string TestClientId = "test-client-id";
        const string TestApiScopes = "api://test-audience/.default";

        OpenIdConnectTestConfiguration configuration;
        MockOidcServer mockOidcServer;

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

        // ── GET api/recoverability/groups ────────────────────────────────────────────

        [Test]
        public async Task GetAllGroups_operator_receives_200()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("alice", ["sc-operator"]);
                    response = await Get("/api/recoverability/groups", token);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task GetAllGroups_no_permission_receives_403()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("dave", ["sc-no-perms"]);
                    response = await Get("/api/recoverability/groups", token);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        [Test]
        public async Task GetAllGroups_deny_decision_is_logged()
        {
            var recordingProvider = new RecordingLoggerProvider();
            CustomizeHostBuilder = hb => hb.Services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(recordingProvider);

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("dave", ["sc-no-perms"]);
                    await Get("/api/recoverability/groups", token);
                    return true;
                })
                .Run();

            Assert.That(recordingProvider.EntriesFor("ServiceControl.Audit"),
                Has.Some.Matches<LogEntry>(e => e.Message.Contains("recoverabilitygroups:view") && e.Message.Contains("deny")));
        }

        [Test]
        public async Task GetAllGroups_allow_decision_is_logged()
        {
            var recordingProvider = new RecordingLoggerProvider();
            CustomizeHostBuilder = hb => hb.Services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(recordingProvider);

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("alice", ["sc-operator"]);
                    await Get("/api/recoverability/groups", token);
                    return true;
                })
                .Run();

            Assert.That(recordingProvider.EntriesFor("ServiceControl.Audit"),
                Has.Some.Matches<LogEntry>(e => e.Message.Contains("recoverabilitygroups:view") && e.Message.Contains("allow")));
        }

        // ── GET api/recoverability/classifiers ────────────────────────────────────────

        [Test]
        public async Task GetClassifiers_operator_receives_200()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("alice", ["sc-operator"]);
                    response = await Get("/api/recoverability/classifiers", token);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task GetClassifiers_no_permission_receives_403()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("dave", ["sc-no-perms"]);
                    response = await Get("/api/recoverability/classifiers", token);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        // ── GET api/recoverability/history ────────────────────────────────────────────

        [Test]
        public async Task GetHistory_operator_receives_200()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("alice", ["sc-operator"]);
                    response = await Get("/api/recoverability/history", token);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task GetHistory_no_permission_receives_403()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("dave", ["sc-no-perms"]);
                    response = await Get("/api/recoverability/history", token);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        // ── POST api/recoverability/groups/{id}/errors/retry (fail-closed) ────────────

        [Test]
        public async Task GroupRetry_operator_unrestricted_receives_2xx()
        {
            var groupId = Guid.NewGuid().ToString("N");
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("alice", ["sc-operator"]);
                    response = await Post($"/api/recoverability/groups/{groupId}/errors/retry", token);
                    return response != null;
                })
                .Run();

            Assert.That((int)response.StatusCode, Is.InRange(200, 299));
        }

        [Test]
        public async Task GroupRetry_no_permission_receives_403()
        {
            var groupId = Guid.NewGuid().ToString("N");
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("bob", ["sc-viewer"]);
                    response = await Post($"/api/recoverability/groups/{groupId}/errors/retry", token);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        [Test]
        public async Task GroupRetry_scoped_user_receives_403_fail_closed()
        {
            // Scoped users are denied fail-closed because groups span multiple queues and
            // cannot be scope-checked per-queue. This is a v1 documented limitation.
            var groupId = Guid.NewGuid().ToString("N");
            HttpResponseMessage response = null;

            using var scopedConfig = new ScopedGroupRbacConfiguration();

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("scoped-op", ["scoped-operator"]);
                    response = await Post($"/api/recoverability/groups/{groupId}/errors/retry", token);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden),
                "A scoped user must be denied fail-closed for group operations");
        }

        // ── POST api/recoverability/groups/{id}/errors/archive ────────────────────────

        [Test]
        public async Task GroupArchive_operator_receives_2xx()
        {
            var groupId = Guid.NewGuid().ToString("N");
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("alice", ["sc-operator"]);
                    response = await Post($"/api/recoverability/groups/{groupId}/errors/archive", token);
                    return response != null;
                })
                .Run();

            Assert.That((int)response.StatusCode, Is.InRange(200, 299));
        }

        [Test]
        public async Task GroupArchive_no_permission_receives_403()
        {
            var groupId = Guid.NewGuid().ToString("N");
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("bob", ["sc-viewer"]);
                    response = await Post($"/api/recoverability/groups/{groupId}/errors/archive", token);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        [Test]
        public async Task GroupArchive_scoped_user_receives_403_fail_closed()
        {
            var groupId = Guid.NewGuid().ToString("N");
            HttpResponseMessage response = null;

            using var scopedConfig = new ScopedGroupRbacConfiguration();

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("scoped-op", ["scoped-operator"]);
                    response = await Post($"/api/recoverability/groups/{groupId}/errors/archive", token);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        // ── POST api/recoverability/groups/{id}/errors/unarchive ──────────────────────

        [Test]
        public async Task GroupUnarchive_operator_receives_2xx()
        {
            var groupId = Guid.NewGuid().ToString("N");
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("alice", ["sc-operator"]);
                    response = await Post($"/api/recoverability/groups/{groupId}/errors/unarchive", token);
                    return response != null;
                })
                .Run();

            Assert.That((int)response.StatusCode, Is.InRange(200, 299));
        }

        [Test]
        public async Task GroupUnarchive_no_permission_receives_403()
        {
            var groupId = Guid.NewGuid().ToString("N");
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("bob", ["sc-viewer"]);
                    response = await Post($"/api/recoverability/groups/{groupId}/errors/unarchive", token);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        [Test]
        public async Task GroupUnarchive_scoped_user_receives_403_fail_closed()
        {
            var groupId = Guid.NewGuid().ToString("N");
            HttpResponseMessage response = null;

            using var scopedConfig = new ScopedGroupRbacConfiguration();

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("scoped-op", ["scoped-operator"]);
                    response = await Post($"/api/recoverability/groups/{groupId}/errors/unarchive", token);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        // ── OIDC disabled ─────────────────────────────────────────────────────────────

        [Test]
        public async Task GroupRetry_oidc_disabled_accepts_without_token()
        {
            configuration?.Dispose();
            configuration = new OpenIdConnectTestConfiguration(ServiceControlInstanceType.Primary)
                .WithAuthenticationDisabled();

            var groupId = Guid.NewGuid().ToString("N");
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    response = await OpenIdConnectAssertions.SendRequestWithoutAuth(HttpClient, HttpMethod.Post,
                        $"/api/recoverability/groups/{groupId}/errors/retry");
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.Forbidden));
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        Task<HttpResponseMessage> Get(string path, string token) =>
            OpenIdConnectAssertions.SendRequestWithBearerToken(HttpClient, HttpMethod.Get, path, token);

        Task<HttpResponseMessage> Post(string path, string token) =>
            OpenIdConnectAssertions.SendRequestWithBearerToken(HttpClient, HttpMethod.Post, path, token);

        /// <summary>
        /// RBAC config with a scoped-operator that has scoped (queue-restricted) grants
        /// for group retry/archive/unarchive. Used to verify the fail-closed behaviour.
        /// </summary>
        sealed class ScopedGroupRbacConfiguration : IDisposable
        {
            readonly string tempYamlPath;
            bool disposed;

            public ScopedGroupRbacConfiguration()
            {
                const string scopedYaml = """
                    schemaVersion: 1
                    roles:
                      sc-admin:
                        bindings: [ "role:sc-admin" ]
                        permissions: [ "*" ]
                      sc-operator:
                        bindings: [ "role:sc-operator" ]
                        permissions:
                          - "recoverabilitygroups:retry"
                          - "recoverabilitygroups:archive"
                          - "recoverabilitygroups:unarchive"
                      sc-viewer:
                        bindings: [ "role:sc-viewer" ]
                        permissions:
                          - "recoverabilitygroups:view"
                      scoped-operator:
                        bindings: [ "role:scoped-operator" ]
                        permissions:
                          - permission: "recoverabilitygroups:retry"
                            scope: { allow: ["Sales.*"] }
                          - permission: "recoverabilitygroups:archive"
                            scope: { allow: ["Sales.*"] }
                          - permission: "recoverabilitygroups:unarchive"
                            scope: { allow: ["Sales.*"] }
                    """;

                tempYamlPath = Path.Combine(Path.GetTempPath(), $"rbac-test-{Guid.NewGuid():N}.yaml");
                File.WriteAllText(tempYamlPath, scopedYaml);
                Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_RBACPOLICYFILE", tempYamlPath);
            }

            public void Dispose()
            {
                if (!disposed)
                {
                    Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_RBACPOLICYFILE", null);
                    if (File.Exists(tempYamlPath))
                    {
                        File.Delete(tempYamlPath);
                    }
                    disposed = true;
                }
            }
        }

        class Context : ScenarioContext;
    }
}
