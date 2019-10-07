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
            var publicApi = ApiGenerator.GeneratePublicApi(typeof(Bootstrapper).Assembly);
            Approver.Verify(publicApi);
        }

        [Test]
        public void RootPathValue()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost");
            request.Properties.Add(HttpPropertyKeys.RequestContextKey, new HttpRequestContext {VirtualPathRoot = "/"});

            var controller = new RootController(new LoggingSettings("testEndpoint"), new Settings())
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

            Approver.Verify(new Settings());
        }
    }
}