namespace ServiceControl.UnitTests.API
{
    using System.Collections.Generic;
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
    using ServiceControl.Audit.Persistence.InMemory;
    using ServiceControl.Transports.Learning;

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
            request.Options.TryAdd(HttpPropertyKeys.RequestContextKey, new HttpRequestContext { VirtualPathRoot = "/" });

            var settings = CreateTestSettings();

            var controller = new RootController(new LoggingSettings("testEndpoint"), settings)
            {
                Url = new UrlHelper(request)
            };

            var result = controller.Urls();

            Approver.Verify(result.Value);
        }

        [Test]
        public void PlatformSampleSettings()
        {
            //HINT: Particular.PlatformSample includes a parameterized version of the ServiceControl.exe.config file.
            //If any changes have been made to settings, this may break the embedded config in that project, which may need to be updated.
            var settings = CreateTestSettings();

            settings.LicenseFileText = null;

            Approver.Verify(settings);
        }

        static Settings CreateTestSettings()
        {
            return new Settings(
                Settings.DEFAULT_SERVICE_NAME,
                typeof(LearningTransportCustomization).AssemblyQualifiedName,
                typeof(InMemoryPersistence).AssemblyQualifiedName);
        }
    }
}