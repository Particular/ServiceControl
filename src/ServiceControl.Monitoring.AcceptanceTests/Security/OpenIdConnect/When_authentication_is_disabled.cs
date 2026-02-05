namespace ServiceControl.Monitoring.AcceptanceTests.Security.OpenIdConnect
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.OpenIdConnect;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// Authentication Disabled (Default)
    /// When Authentication.Enabled is false (default), all API endpoints should be accessible
    /// without authentication. Requests should not require Bearer tokens.
    /// </summary>
    class When_authentication_is_disabled : AcceptanceTest
    {
        OpenIdConnectTestConfiguration configuration;

        [SetUp]
        public void ConfigureAuth() =>
            configuration = new OpenIdConnectTestConfiguration(ServiceControlInstanceType.Monitoring)
                .WithAuthenticationDisabled();

        [TearDown]
        public void CleanupAuth() => configuration?.Dispose();

        [Test]
        public async Task Should_allow_requests_without_authentication()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    // Use /monitored-endpoints to test an endpoint that would require auth if enabled
                    response = await OpenIdConnectAssertions.SendRequestWithoutAuth(
                        HttpClient,
                        HttpMethod.Get,
                        "/monitored-endpoints");
                    return response != null;
                })
                .Run();

            OpenIdConnectAssertions.AssertNoAuthenticationRequired(response);
        }

        class Context : ScenarioContext
        {
        }
    }
}
