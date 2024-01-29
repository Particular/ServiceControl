namespace ServiceControl.UnitTests.API
{
    using Audit.Infrastructure.Settings;
    using Audit.Infrastructure.WebApi;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Mvc.Routing;
    using Microsoft.AspNetCore.Routing;
    using NUnit.Framework;
    using Particular.Approvals;
    using PublicApiGenerator;
    using ServiceControl.Audit.Persistence.InMemory;
    using ServiceControl.Transports.Learning;

    [TestFixture]
    class APIApprovals
    {
        // TODO: This test is probably no longer a good idea with all the controllers being public and this test probably
        // previously intending to "abuse" api approvals to verify some sort of contract shared between the primary and the secondary
        [Test]
        public void PublicClr()
        {
            var publicApi = typeof(WebApiHostBuilderExtensions).Assembly.GeneratePublicApi(new ApiGeneratorOptions
            {
                ExcludeAttributes = new[] { "System.Reflection.AssemblyMetadataAttribute" }
            });
            Approver.Verify(publicApi);
        }

        [Test]
        public void RootPathValue()
        {
            var httpContext = new DefaultHttpContext { Request = { Scheme = "http", Host = new HostString("localhost") } };
            var actionContext = new ActionContext { HttpContext = httpContext, RouteData = new RouteData(), ActionDescriptor = new ControllerActionDescriptor() };
            var controllerContext = new ControllerContext(actionContext);

            var settings = CreateTestSettings();

            var controller = new RootController(
                new LoggingSettings("testEndpoint"),
                settings
            )
            {
                ControllerContext = controllerContext,
                Url = new UrlHelper(actionContext)
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

        static Settings CreateTestSettings() =>
            new(
                Settings.DEFAULT_SERVICE_NAME,
                typeof(LearningTransportCustomization).AssemblyQualifiedName,
                typeof(InMemoryPersistence).AssemblyQualifiedName);
    }
}