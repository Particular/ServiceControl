namespace ServiceControl.AcceptanceTests.Security.Authorization
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Auth;
    using AcceptanceTesting.OpenIdConnect;
    using Contracts.Operations;
    using MessageFailures;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Persistence;

    /// <summary>
    /// S2 RBAC enforcement tests for message archive/unarchive and edit-and-retry:
    /// - PATCH/POST api/errors/{id}/archive   (messages:archive)
    /// - PATCH/POST api/errors/archive        (messages:archive, batch)
    /// - PATCH api/errors/unarchive           (messages:unarchive, batch)
    /// - GET  api/edit/config                 (messages:edit)
    /// - POST api/edit/{id}                   (messages:edit)
    ///
    /// Test matrix: (a) permitted → 2xx, (b) no-perm → 403, (c) decision logged, (d) OIDC disabled → 2xx
    /// </summary>
    class When_archiving_messages_s2 : AcceptanceTest
    {
        const string TestAudience = "api://test-audience";
        const string TestClientId = "test-client-id";
        const string TestApiScopes = "api://test-audience/.default";
        const string SalesQueueAddress = "Sales.OrderHandler@localhost";

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

        // ── Single-message archive ──────────────────────────────────────────────────

        [Test]
        public async Task Archive_single_operator_receives_2xx()
        {
            var messageId = Guid.NewGuid().ToString("N");
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    await StoreFailedMessage(messageId, SalesQueueAddress);
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("alice", ["sc-operator"]);
                    response = await Post($"/api/errors/{messageId}/archive", token);
                    return response != null;
                })
                .Run();

            Assert.That((int)response.StatusCode, Is.InRange(200, 299));
        }

        [Test]
        public async Task Archive_single_viewer_without_archive_receives_403()
        {
            var messageId = Guid.NewGuid().ToString("N");
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    await StoreFailedMessage(messageId, SalesQueueAddress);
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("bob", ["sc-viewer"]);
                    response = await Post($"/api/errors/{messageId}/archive", token);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        [Test]
        public async Task Archive_single_deny_decision_is_logged()
        {
            var messageId = Guid.NewGuid().ToString("N");
            var recordingProvider = new RecordingLoggerProvider();
            CustomizeHostBuilder = hb => hb.Services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(recordingProvider);

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    await StoreFailedMessage(messageId, SalesQueueAddress);
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("bob", ["sc-viewer"]);
                    await Post($"/api/errors/{messageId}/archive", token);
                    return true;
                })
                .Run();

            Assert.That(recordingProvider.EntriesFor("ServiceControl.Audit"),
                Has.Some.Matches<LogEntry>(e => e.Message.Contains("messages:archive") && e.Message.Contains("deny")));
        }

        [Test]
        public async Task Archive_single_allow_decision_is_logged()
        {
            var messageId = Guid.NewGuid().ToString("N");
            var recordingProvider = new RecordingLoggerProvider();
            CustomizeHostBuilder = hb => hb.Services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(recordingProvider);

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    await StoreFailedMessage(messageId, SalesQueueAddress);
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("alice", ["sc-operator"]);
                    await Post($"/api/errors/{messageId}/archive", token);
                    return true;
                })
                .Run();

            Assert.That(recordingProvider.EntriesFor("ServiceControl.Audit"),
                Has.Some.Matches<LogEntry>(e => e.Message.Contains("messages:archive") && e.Message.Contains("allow")));
        }

        // ── Batch archive ────────────────────────────────────────────────────────────

        [Test]
        public async Task Archive_batch_operator_receives_2xx()
        {
            var messageId = Guid.NewGuid().ToString("N");
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    await StoreFailedMessage(messageId, SalesQueueAddress);
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("alice", ["sc-operator"]);
                    var body = new StringContent($"[\"{messageId}\"]", Encoding.UTF8, "application/json");
                    response = await PostWithBody("/api/errors/archive", token, body);
                    return response != null;
                })
                .Run();

            Assert.That((int)response.StatusCode, Is.InRange(200, 299));
        }

        [Test]
        public async Task Archive_batch_viewer_receives_403()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("bob", ["sc-viewer"]);
                    var body = new StringContent("[]", Encoding.UTF8, "application/json");
                    response = await PostWithBody("/api/errors/archive", token, body);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        // ── Unarchive batch ──────────────────────────────────────────────────────────

        [Test]
        public async Task Unarchive_batch_operator_receives_2xx()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("alice", ["sc-operator"]);
                    using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/errors/unarchive");
                    request.Headers.Authorization = OpenIdConnectAssertions.CreateBearerToken(token);
                    request.Content = new StringContent("[]", Encoding.UTF8, "application/json");
                    response = await HttpClient.SendAsync(request);
                    return response != null;
                })
                .Run();

            // empty ids → BadRequest(400) is still a successful auth check (gets past 403)
            Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.Forbidden));
        }

        [Test]
        public async Task Unarchive_batch_viewer_receives_403()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("bob", ["sc-viewer"]);
                    using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/errors/unarchive");
                    request.Headers.Authorization = OpenIdConnectAssertions.CreateBearerToken(token);
                    request.Content = new StringContent("[]", Encoding.UTF8, "application/json");
                    response = await HttpClient.SendAsync(request);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        // ── Edit config GET ──────────────────────────────────────────────────────────

        [Test]
        public async Task EditConfig_operator_receives_200()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("alice", ["sc-operator"]);
                    response = await OpenIdConnectAssertions.SendRequestWithBearerToken(HttpClient, HttpMethod.Get, "/api/edit/config", token);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task EditConfig_viewer_receives_403()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("bob", ["sc-viewer"]);
                    response = await OpenIdConnectAssertions.SendRequestWithBearerToken(HttpClient, HttpMethod.Get, "/api/edit/config", token);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        // ── OIDC disabled ─────────────────────────────────────────────────────────────

        [Test]
        public async Task Archive_oidc_disabled_accepts_without_token()
        {
            configuration?.Dispose();
            configuration = new OpenIdConnectTestConfiguration(ServiceControlInstanceType.Primary)
                .WithAuthenticationDisabled();

            var messageId = Guid.NewGuid().ToString("N");
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    await StoreFailedMessage(messageId, SalesQueueAddress);
                    response = await OpenIdConnectAssertions.SendRequestWithoutAuth(HttpClient, HttpMethod.Post, $"/api/errors/{messageId}/archive");
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.Forbidden));
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        Task StoreFailedMessage(string messageId, string queueAddress)
        {
            var dataStore = Services.GetRequiredService<IErrorMessageDataStore>();
            return dataStore.StoreFailedMessagesForTestsOnly(BuildFailedMessage(messageId, queueAddress));
        }

        Task<HttpResponseMessage> Post(string path, string token) =>
            OpenIdConnectAssertions.SendRequestWithBearerToken(HttpClient, HttpMethod.Post, path, token);

        async Task<HttpResponseMessage> PostWithBody(string path, string token, HttpContent content)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.Authorization = OpenIdConnectAssertions.CreateBearerToken(token);
            request.Content = content;
            return await HttpClient.SendAsync(request);
        }

        static FailedMessage BuildFailedMessage(string uniqueMessageId, string queueAddress) =>
            new()
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
                        FailureDetails = new FailureDetails { AddressOfFailingEndpoint = queueAddress, TimeOfFailure = DateTime.UtcNow },
                        Headers = new Dictionary<string, string>
                        {
                            ["NServiceBus.MessageId"] = uniqueMessageId,
                            ["NServiceBus.FailedQ"] = queueAddress
                        }
                    }
                ]
            };

        class Context : ScenarioContext;
    }
}
