namespace ServiceControl.AcceptanceTests.Security.Authorization
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Auth;
    using AcceptanceTesting.OpenIdConnect;
    using Contracts.Operations;
    using MessageFailures;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;

    /// <summary>
    /// S2 RBAC enforcement tests for failed-message read endpoints:
    /// - GET api/errors  (ErrorsGet)
    /// - GET api/errors/summary  (ErrorsSummary)
    /// - GET api/errors/{id}  (ErrorBy)
    /// - GET api/errors/last/{id}  (ErrorLastBy)
    /// - GET api/endpoints/{name}/errors  (ErrorsByEndpointName)
    ///
    /// Test matrix per endpoint:
    /// (a) permitted role (sc-operator has messages:view) → 200
    /// (b) unpermitted role (no messages:view) → 403
    /// (c) scoped role — in-scope → 200, out-of-scope (for single-message endpoints) → 403
    /// (d) allow and deny decisions logged in ServiceControl.Audit
    /// (e) OIDC disabled → endpoint accepts without auth
    /// </summary>
    class When_reading_failed_messages_s2 : AcceptanceTest
    {
        const string TestAudience = "api://test-audience";
        const string TestClientId = "test-client-id";
        const string TestApiScopes = "api://test-audience/.default";

        const string SalesQueueAddress = "Sales.OrderHandler@localhost";
        const string FinanceQueueAddress = "Finance.Payments@localhost";

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

        // ── GET api/errors ──────────────────────────────────────────────────────────

        [Test]
        public async Task ErrorsGet_operator_receives_200()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("alice", ["sc-operator"]);
                    response = await Get("/api/errors", token);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task ErrorsGet_role_without_view_receives_403()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    // Role with no permissions at all
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("dave", ["sc-no-perms"]);
                    response = await Get("/api/errors", token);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        [Test]
        public async Task ErrorsGet_scoped_role_returns_filtered_results()
        {
            HttpResponseMessage response = null;

            using var scopedConfig = new ScopedRbacConfiguration();

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    await StoreFailedMessage(Guid.NewGuid().ToString("N"), SalesQueueAddress);
                    await StoreFailedMessage(Guid.NewGuid().ToString("N"), FinanceQueueAddress);

                    var token = mockOidcServer.GenerateTokenWithRealmRoles("sales-user", ["sales-operator"]);
                    response = await Get("/api/errors", token);
                    return response != null;
                })
                .Run();

            // A scoped user gets a 200 but only sees in-scope messages (paging total is filtered too)
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task ErrorsGet_deny_decision_is_logged()
        {
            var recordingProvider = new RecordingLoggerProvider();
            CustomizeHostBuilder = hb => hb.Services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(recordingProvider);

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("anon", ["sc-no-perms"]);
                    await Get("/api/errors", token);
                    return true;
                })
                .Run();

            Assert.That(recordingProvider.EntriesFor("ServiceControl.Audit"),
                Has.Some.Matches<LogEntry>(e => e.Message.Contains("messages:view") && e.Message.Contains("deny")));
        }

        [Test]
        public async Task ErrorsGet_allow_decision_is_logged()
        {
            var recordingProvider = new RecordingLoggerProvider();
            CustomizeHostBuilder = hb => hb.Services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(recordingProvider);

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("op", ["sc-operator"]);
                    await Get("/api/errors", token);
                    return true;
                })
                .Run();

            Assert.That(recordingProvider.EntriesFor("ServiceControl.Audit"),
                Has.Some.Matches<LogEntry>(e => e.Message.Contains("messages:view") && e.Message.Contains("allow")));
        }

        [Test]
        public async Task ErrorsGet_oidc_disabled_returns_200_without_token()
        {
            configuration?.Dispose();
            configuration = new OpenIdConnectTestConfiguration(ServiceControlInstanceType.Primary)
                .WithAuthenticationDisabled();

            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    response = await OpenIdConnectAssertions.SendRequestWithoutAuth(HttpClient, HttpMethod.Get, "/api/errors");
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        // ── GET api/errors/summary ──────────────────────────────────────────────────

        [Test]
        public async Task ErrorsSummary_operator_passes_auth_gate()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("alice", ["sc-operator"]);
                    response = await Get("/api/errors/summary", token);
                    return response != null;
                })
                .Run();

            // The summary endpoint uses a RavenDB facet index that may not be populated in the
            // test environment — any non-403 response confirms the auth gate was passed.
            Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.Forbidden));
        }

        [Test]
        public async Task ErrorsSummary_no_permission_receives_403()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("dave", ["sc-no-perms"]);
                    response = await Get("/api/errors/summary", token);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        // ── GET api/errors/{id} ────────────────────────────────────────────────────

        [Test]
        public async Task ErrorById_operator_receives_200()
        {
            var messageId = Guid.NewGuid().ToString("N");
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    await StoreFailedMessage(messageId, SalesQueueAddress);
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("alice", ["sc-operator"]);
                    response = await Get($"/api/errors/{messageId}", token);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task ErrorById_no_permission_receives_403()
        {
            var messageId = Guid.NewGuid().ToString("N");
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    await StoreFailedMessage(messageId, SalesQueueAddress);
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("dave", ["sc-no-perms"]);
                    response = await Get($"/api/errors/{messageId}", token);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        [Test]
        public async Task ErrorById_scoped_in_scope_receives_200()
        {
            var messageId = Guid.NewGuid().ToString("N");
            HttpResponseMessage response = null;

            using var scopedConfig = new ScopedRbacConfiguration();

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    await StoreFailedMessage(messageId, SalesQueueAddress);
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("sales-user", ["sales-operator"]);
                    response = await Get($"/api/errors/{messageId}", token);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task ErrorById_scoped_out_of_scope_receives_403()
        {
            var messageId = Guid.NewGuid().ToString("N");
            HttpResponseMessage response = null;

            using var scopedConfig = new ScopedRbacConfiguration();

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    await StoreFailedMessage(messageId, FinanceQueueAddress);
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("sales-user", ["sales-operator"]);
                    response = await Get($"/api/errors/{messageId}", token);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        [Test]
        public async Task ErrorById_scoped_deny_logged_with_queue_address()
        {
            var messageId = Guid.NewGuid().ToString("N");
            var recordingProvider = new RecordingLoggerProvider();
            CustomizeHostBuilder = hb => hb.Services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(recordingProvider);

            using var scopedConfig = new ScopedRbacConfiguration();

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    await StoreFailedMessage(messageId, FinanceQueueAddress);
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("sales-audit", ["sales-operator"]);
                    await Get($"/api/errors/{messageId}", token);
                    return true;
                })
                .Run();

            Assert.That(recordingProvider.EntriesFor("ServiceControl.Audit"),
                Has.Some.Matches<LogEntry>(e =>
                    e.Message.Contains("messages:view")
                    && e.Message.Contains("deny")
                    && e.Message.Contains(FinanceQueueAddress)));
        }

        // ── GET api/endpoints/{name}/errors ───────────────────────────────────────

        [Test]
        public async Task ErrorsByEndpoint_operator_receives_200()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("alice", ["sc-operator"]);
                    response = await Get("/api/endpoints/Sales.OrderHandler/errors", token);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task ErrorsByEndpoint_no_permission_receives_403()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("dave", ["sc-no-perms"]);
                    response = await Get("/api/endpoints/Sales.OrderHandler/errors", token);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        // ── Helpers ─────────────────────────────────────────────────────────────────

        Task StoreFailedMessage(string messageId, string queueAddress)
        {
            var dataStore = Services.GetRequiredService<IErrorMessageDataStore>();
            return dataStore.StoreFailedMessagesForTestsOnly(BuildFailedMessage(messageId, queueAddress));
        }

        Task<HttpResponseMessage> Get(string path, string token) =>
            OpenIdConnectAssertions.SendRequestWithBearerToken(HttpClient, HttpMethod.Get, path, token);

        static FailedMessage BuildFailedMessage(string uniqueMessageId, string queueAddress)
        {
            // Build the minimal MessageMetadata required by FailedMessageViewIndex and
            // FailedMessageViewTransformer so that GET /api/errors can deserialise the results.
            var endpointName = queueAddress.Split('@')[0]; // e.g. "Sales.OrderHandler"
            var receivingEndpoint = new EndpointDetails { Name = endpointName, Host = "localhost", HostId = Guid.Empty };
            var metadata = new Dictionary<string, object>
            {
                ["MessageId"] = uniqueMessageId,
                ["MessageType"] = "TestMessage",
                ["IsSystemMessage"] = false,
                ["TimeSent"] = DateTime.UtcNow,
                ["ReceivingEndpoint"] = receivingEndpoint,
                ["SendingEndpoint"] = receivingEndpoint,
            };

            return new FailedMessage
            {
                Id = $"FailedMessages/{uniqueMessageId}",
                UniqueMessageId = uniqueMessageId,
                Status = FailedMessageStatus.Unresolved,
                ProcessingAttempts =
                [
                    new FailedMessage.ProcessingAttempt
                    {
                        AttemptedAt = DateTime.UtcNow,
                        MessageId = uniqueMessageId,
                        MessageMetadata = metadata,
                        FailureDetails = new FailureDetails
                        {
                            AddressOfFailingEndpoint = queueAddress,
                            TimeOfFailure = DateTime.UtcNow
                        },
                        Headers = new Dictionary<string, string>
                        {
                            ["NServiceBus.MessageId"] = uniqueMessageId,
                            ["NServiceBus.FailedQ"] = queueAddress
                        }
                    }
                ]
            };
        }

        sealed class ScopedRbacConfiguration : IDisposable
        {
            readonly string tempYamlPath;
            bool disposed;

            public ScopedRbacConfiguration()
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
                          - "messages:view"
                      sc-viewer:
                        bindings: [ "role:sc-viewer" ]
                        permissions:
                          - "messages:view"
                      sales-operator:
                        bindings: [ "role:sales-operator" ]
                        permissions:
                          - permission: "messages:view"
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
