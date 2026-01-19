namespace ServiceControl.AcceptanceTests.Security.OpenIdConnect
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.OpenIdConnect;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// ServicePulse Authority Override
    /// When Authentication.ServicePulse.Authority is configured, the authentication configuration
    /// endpoint should return the overridden authority for ServicePulse to use instead of the
    /// main authority URL.
    /// </summary>
    class When_service_pulse_authority_override_is_configured : AcceptanceTest
    {
        OpenIdConnectTestConfiguration configuration;

        const string MainAuthority = "https://login.main.example.com/tenant-id/v2.0";
        const string ServicePulseAuthority = "https://login.pulse.example.com/tenant-id/v2.0";
        const string TestAudience = "api://test-audience";
        const string TestClientId = "test-client-id";
        const string TestApiScopes = "api://test-audience/.default";

        [SetUp]
        public void ConfigureAuth() =>
            configuration = new OpenIdConnectTestConfiguration(ServiceControlInstanceType.Primary)
                .WithConfigurationValidationDisabled()
                .WithAuthenticationEnabled()
                .WithAuthority(MainAuthority)
                .WithAudience(TestAudience)
                .WithServicePulseClientId(TestClientId)
                .WithServicePulseApiScopes(TestApiScopes)
                .WithServicePulseAuthority(ServicePulseAuthority)
                .WithRequireHttpsMetadata(false);

        [TearDown]
        public void CleanupAuth() => configuration?.Dispose();

        [Test]
        public async Task Should_return_service_pulse_authority_in_configuration()
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
                expectedAuthority: ServicePulseAuthority,
                expectedAudience: TestAudience,
                expectedApiScopes: TestApiScopes);
        }

        class Context : ScenarioContext
        {
        }
    }
}
