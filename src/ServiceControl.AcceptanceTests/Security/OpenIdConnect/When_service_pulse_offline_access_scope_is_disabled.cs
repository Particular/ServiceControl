namespace ServiceControl.AcceptanceTests.Security.OpenIdConnect
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.OpenIdConnect;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// ServicePulse Offline Access Scope Opt-Out
    /// When Authentication.ServicePulse.OfflineAccessScopeEnabled is false, the composed scope
    /// string returned by the authentication configuration endpoint should omit offline_access,
    /// so ServicePulse does not request a scope the identity provider disallows.
    /// </summary>
    class When_service_pulse_offline_access_scope_is_disabled : AcceptanceTest
    {
        OpenIdConnectTestConfiguration configuration;

        const string TestAuthority = "https://login.example.com/tenant-id/v2.0";
        const string TestAudience = "api://test-audience";
        const string TestClientId = "test-client-id";
        const string TestApiScopes = "api://test-audience/.default";

        [SetUp]
        public void ConfigureAuth() =>
            configuration = new OpenIdConnectTestConfiguration(ServiceControlInstanceType.Primary)
                .WithConfigurationValidationDisabled()
                .WithAuthenticationEnabled()
                .WithAuthority(TestAuthority)
                .WithAudience(TestAudience)
                .WithServicePulseClientId(TestClientId)
                .WithServicePulseApiScopes(TestApiScopes)
                .WithServicePulseOfflineAccessScopeEnabled(false)
                .WithRequireHttpsMetadata(false);

        [TearDown]
        public void CleanupAuth() => configuration?.Dispose();

        [Test]
        public async Task Should_omit_offline_access_from_composed_scopes()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    response = await OpenIdConnectAssertions.SendRequestWithoutAuth(
                        HttpClient,
                        HttpMethod.Get,
                        "/api/authentication/configuration");
                    return response != null;
                })
                .Run();

            await OpenIdConnectAssertions.AssertAuthConfigurationResponse(
                response,
                expectedEnabled: true,
                expectedClientId: TestClientId,
                expectedAudience: TestAudience,
                expectedApiScopes: TestApiScopes,
                expectedScopes: $"{TestApiScopes} openid profile email");
        }

        class Context : ScenarioContext;
    }
}
