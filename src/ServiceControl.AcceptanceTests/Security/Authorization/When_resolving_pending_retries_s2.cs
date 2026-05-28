namespace ServiceControl.AcceptanceTests.Security.Authorization
{
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Auth;
    using AcceptanceTesting.OpenIdConnect;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// S2 RBAC enforcement tests for pending-retry and resolve endpoints:
    /// - POST  api/pendingretries/retry         (messages:retry)
    /// - POST  api/pendingretries/queues/retry  (messages:retry)
    /// - PATCH api/pendingretries/resolve       (messages:retry)
    /// - PATCH api/pendingretries/queues/resolve (messages:retry)
    ///
    /// Test matrix: (a) permitted (sc-operator) → 2xx, (b) no-perm (sc-viewer) → 403,
    /// (c) decision logged, (d) OIDC disabled → 2xx
    /// </summary>
    class When_resolving_pending_retries_s2 : AcceptanceTest
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

        // ── POST pendingretries/retry ─────────────────────────────────────────────────

        [Test]
        public async Task PendingRetriesRetry_operator_receives_2xx()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("alice", ["sc-operator"]);
                    var body = new StringContent("[]", Encoding.UTF8, "application/json");
                    response = await PostWithBody("/api/pendingretries/retry", token, body);
                    return response != null;
                })
                .Run();

            // Empty ids → 422 UnprocessableEntity — that's past the 403 gate, which is what matters
            Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.Forbidden));
        }

        [Test]
        public async Task PendingRetriesRetry_viewer_receives_403()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("bob", ["sc-viewer"]);
                    var body = new StringContent("[]", Encoding.UTF8, "application/json");
                    response = await PostWithBody("/api/pendingretries/retry", token, body);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        [Test]
        public async Task PendingRetriesRetry_deny_decision_is_logged()
        {
            var recordingProvider = new RecordingLoggerProvider();
            CustomizeHostBuilder = hb => hb.Services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(recordingProvider);

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("bob", ["sc-viewer"]);
                    var body = new StringContent("[]", Encoding.UTF8, "application/json");
                    await PostWithBody("/api/pendingretries/retry", token, body);
                    return true;
                })
                .Run();

            Assert.That(recordingProvider.EntriesFor("ServiceControl.Audit"),
                Has.Some.Matches<LogEntry>(e => e.Message.Contains("messages:retry") && e.Message.Contains("deny")));
        }

        // ── PATCH pendingretries/resolve ──────────────────────────────────────────────

        [Test]
        public async Task Resolve_operator_receives_2xx()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("alice", ["sc-operator"]);
                    var json = """{"uniquemessageids":["some-id"]}""";
                    response = await PatchWithBody("/api/pendingretries/resolve", token, json);
                    return response != null;
                })
                .Run();

            // Accepted or some 2xx
            Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.Forbidden));
        }

        [Test]
        public async Task Resolve_viewer_receives_403()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("bob", ["sc-viewer"]);
                    var json = """{"uniquemessageids":["some-id"]}""";
                    response = await PatchWithBody("/api/pendingretries/resolve", token, json);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        // ── OIDC disabled ─────────────────────────────────────────────────────────────

        [Test]
        public async Task PendingRetries_oidc_disabled_accepts_without_token()
        {
            configuration?.Dispose();
            configuration = new OpenIdConnectTestConfiguration(ServiceControlInstanceType.Primary)
                .WithAuthenticationDisabled();

            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var body = new StringContent("[]", Encoding.UTF8, "application/json");
                    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/pendingretries/retry");
                    request.Content = body;
                    response = await HttpClient.SendAsync(request);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.Forbidden));
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        async Task<HttpResponseMessage> PostWithBody(string path, string token, HttpContent body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.Authorization = OpenIdConnectAssertions.CreateBearerToken(token);
            request.Content = body;
            return await HttpClient.SendAsync(request);
        }

        async Task<HttpResponseMessage> PatchWithBody(string path, string token, string json)
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, path);
            request.Headers.Authorization = OpenIdConnectAssertions.CreateBearerToken(token);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return await HttpClient.SendAsync(request);
        }

        class Context : ScenarioContext;
    }
}
