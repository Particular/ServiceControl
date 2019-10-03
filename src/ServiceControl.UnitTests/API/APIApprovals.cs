namespace ServiceControl.UnitTests.API
{
    using System.Net.Http;
    using System.Web.Http.Controllers;
    using System.Web.Http.Hosting;
    using System.Web.Http.Routing;
    using NUnit.Framework;
    using Particular.Approvals;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Licensing;
    using PublicApiGenerator;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.WebApi;
    using ServiceControlInstaller.Engine.Instances;
    using Transports;

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
        public void ServiceControlTransport()
        {
            var serviceControlTransportApi = ApiGenerator.GeneratePublicApi(typeof(TransportSettings).Assembly);
            Approver.Verify(serviceControlTransportApi);
        }

        [Test]
        public void RootPathValue()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost");
            request.Properties.Add(HttpPropertyKeys.RequestContextKey, new HttpRequestContext {VirtualPathRoot = "/"});

            var controller = new RootController(new ActiveLicense {IsValid = true}, new LoggingSettings("testEndpoint"), new Settings())
            {
                Url = new UrlHelper(request)
            };

            var result = controller.Urls();

            Approver.Verify(result.Content);
        }

        [Test]
        public void TransportNames()
        {
            //HINT: Those names are used in PowerShell scripts thus constitute a public api.
            //Also Particular.PlatformSamples relies on it to specify the learning transport.
            var transportNamesType = typeof(TransportNames);

            var publicTransportNames = ApiGenerator.GeneratePublicApi(transportNamesType.Assembly, new[] {transportNamesType});

            Approver.Verify(publicTransportNames);
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