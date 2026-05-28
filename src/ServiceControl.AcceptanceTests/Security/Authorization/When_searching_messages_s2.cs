namespace ServiceControl.AcceptanceTests.Security.Authorization
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.OpenIdConnect;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// S2 RBAC enforcement tests for the messages search and conversation endpoints:
    /// - GET api/messages                        (messages:view)
    /// - GET api/messages2                       (messages:view)
    /// - GET api/messages/search                 (messages:view)
    /// - GET api/messages/search/{keyword}       (messages:view)
    /// - GET api/conversations/{conversationId}  (messages:view)
    /// - GET api/endpoints/{ep}/messages         (messages:view)
    /// - GET api/endpoints/{ep}/messages/search  (messages:view)
    ///
    /// Test matrix: (a) permitted → 200, (b) no-perm → 403, (c) OIDC disabled → 200
    /// </summary>
    class When_searching_messages_s2 : AcceptanceTest
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

        static readonly (string path, string description)[] MessagesViewPaths =
        [
            ("/api/messages", "GET api/messages"),
            ("/api/messages2", "GET api/messages2"),
            ("/api/messages/search?q=test", "GET api/messages/search"),
            ("/api/messages/search/test", "GET api/messages/search/{keyword}"),
            ("/api/conversations/conv-123", "GET api/conversations/{id}"),
            ("/api/endpoints/Sales.OrderHandler/messages", "GET api/endpoints/{ep}/messages"),
            ("/api/endpoints/Sales.OrderHandler/messages/search?q=x", "GET api/endpoints/{ep}/messages/search"),
            ("/api/endpoints/Sales.OrderHandler/messages/search/x", "GET api/endpoints/{ep}/messages/search/{keyword}"),
        ];

        [Test]
        [TestCaseSource(nameof(MessagesViewPaths))]
        public async Task Operator_receives_200_for_messages_view_path((string path, string description) testCase)
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("alice", ["sc-operator"]);
                    response = await OpenIdConnectAssertions.SendRequestWithBearerToken(
                        HttpClient, HttpMethod.Get, testCase.path, token);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                $"{testCase.description} should return 200 for sc-operator");
        }

        [Test]
        [TestCaseSource(nameof(MessagesViewPaths))]
        public async Task User_without_permission_receives_403_for_messages_view_path((string path, string description) testCase)
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var token = mockOidcServer.GenerateTokenWithRealmRoles("dave", ["sc-no-perms"]);
                    response = await OpenIdConnectAssertions.SendRequestWithBearerToken(
                        HttpClient, HttpMethod.Get, testCase.path, token);
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden),
                $"{testCase.description} should return 403 for user without messages:view");
        }

        [Test]
        public async Task Messages_oidc_disabled_returns_200_without_token()
        {
            configuration?.Dispose();
            configuration = new OpenIdConnectTestConfiguration(ServiceControlInstanceType.Primary)
                .WithAuthenticationDisabled();

            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    response = await OpenIdConnectAssertions.SendRequestWithoutAuth(HttpClient, HttpMethod.Get, "/api/messages");
                    return response != null;
                })
                .Run();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        class Context : ScenarioContext;
    }
}
