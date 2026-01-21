namespace ServiceControl.Audit.AcceptanceTests.Security.Https
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Https;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// HTTPS Redirect Enabled
    /// When RedirectHttpToHttps is true, HTTP requests should be redirected to HTTPS.
    /// </summary>
    class When_https_redirect_is_enabled : AcceptanceTest
    {
        HttpsTestConfiguration configuration;

        [SetUp]
        public void ConfigureHttps() =>
            configuration = new HttpsTestConfiguration(ServiceControlInstanceType.Audit)
                .WithRedirectHttpToHttps()
                .WithHttpsPort(443);

        [TearDown]
        public void CleanupHttps() => configuration?.Dispose();

        [Test]
        public async Task Should_redirect_http_requests_to_https()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    response = await this.GetRaw("/api");
                    return response != null;
                })
                .Run();

            HttpsAssertions.AssertHttpsRedirect(response, expectedPort: 443);
        }

        class Context : ScenarioContext
        {
        }
    }
}
