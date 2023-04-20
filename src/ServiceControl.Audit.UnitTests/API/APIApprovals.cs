namespace ServiceControl.UnitTests.API
{
    using System.Net.Http;
    using System.Web.Http.Controllers;
    using System.Web.Http.Hosting;
    using System.Web.Http.Routing;
    using Audit.Infrastructure;
    using Audit.Infrastructure.Settings;
    using Audit.Infrastructure.WebApi;
    using NUnit.Framework;
    using Particular.Approvals;
    using PublicApiGenerator;

    [TestFixture]
    class APIApprovals
    {
        [Test]
        public void PublicClr()
        {
            var publicApi = typeof(Bootstrapper).Assembly.GeneratePublicApi(new ApiGeneratorOptions
            {
                ExcludeAttributes = new[] { "System.Reflection.AssemblyMetadataAttribute" }
            });
            Approver.Verify(publicApi);
        }

        [Test]
        public void RootPathValue()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost");
            request.Properties.Add(HttpPropertyKeys.RequestContextKey, new HttpRequestContext { VirtualPathRoot = "/" });

            var controller = new RootController(new LoggingSettings("testEndpoint"), Settings.FromConfiguration(Settings.DEFAULT_SERVICE_NAME))
            {
                Url = new UrlHelper(request)
            };

            var result = controller.Urls();

            Approver.Verify(result.Content);
        }

        [Test]
        public void PlatformSampleSettings()
        {
            //HINT: Particular.PlatformSample includes a parameterized version of the ServiceControl.exe.config file.
            //If any changes have been made to settings, this may break the embedded config in that project, which may need to be updated.
            var settings = Settings.FromConfiguration(Settings.DEFAULT_SERVICE_NAME);

            settings.LicenseFileText = null;

            Approver.Verify(settings);
        }
    }
}