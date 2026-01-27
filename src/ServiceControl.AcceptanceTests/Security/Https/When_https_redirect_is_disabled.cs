namespace ServiceControl.AcceptanceTests.Security.Https
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Https;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// HTTPS Redirect Disabled (Default)
    /// When RedirectHttpToHttps is false (default), HTTP requests should not be redirected.
    /// </summary>
    class When_https_redirect_is_disabled : AcceptanceTest
    {
        HttpsTestConfiguration configuration;

        [SetUp]
        public void ConfigureHttps() =>
            configuration = new HttpsTestConfiguration(ServiceControlInstanceType.Primary)
                .WithRedirectHttpToHttpsDisabled();

        [TearDown]
        public void CleanupHttps() => configuration?.Dispose();

        [Test]
        public async Task Should_not_redirect_http_requests()
        {
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    response = await this.GetRaw("/api");
                    return response != null;
                })
                .Run();

            HttpsAssertions.AssertNoHttpsRedirect(response);
        }

        class Context : ScenarioContext
        {
        }
    }
}
