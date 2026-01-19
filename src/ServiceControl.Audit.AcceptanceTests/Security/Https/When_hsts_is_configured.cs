namespace ServiceControl.Audit.AcceptanceTests.Security.Https
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Https;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    /// <summary>
    /// HSTS Configuration
    /// Note: HSTS only applies in non-development environments.
    /// In acceptance tests (which run in Development mode), HSTS headers are not sent.
    /// This test verifies that HSTS is correctly NOT applied in development mode.
    /// </summary>
    class When_hsts_is_configured : AcceptanceTest
    {
        HttpsTestConfiguration configuration;

        [SetUp]
        public void ConfigureHttps() =>
            configuration = new HttpsTestConfiguration(ServiceControlInstanceType.Audit)
                .WithHstsEnabled()
                .WithHstsMaxAge(31536000)
                .WithHstsIncludeSubDomains();

        [TearDown]
        public void CleanupHttps() => configuration?.Dispose();

        [Test]
        public async Task Should_not_include_hsts_header_in_development_mode()
        {
            // HSTS is intentionally NOT applied in development environments
            // This is ASP.NET Core's default behavior to prevent HSTS from being cached
            // by browsers during development, which could cause issues when switching
            // between HTTP and HTTPS during testing.
            HttpResponseMessage response = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    response = await this.GetRaw("/api");
                    return response != null;
                })
                .Run();

            HttpsAssertions.AssertNoHstsHeader(response);
        }

        class Context : ScenarioContext
        {
        }
    }
}
