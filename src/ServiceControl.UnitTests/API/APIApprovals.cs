namespace ServiceControl.UnitTests.API
{
    using LightInject;
    using Nancy;
    using Nancy.Testing;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using Particular.Approvals;
    using Particular.ServiceControl.Licensing;
    using PublicApiGenerator;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using System;
    using System.Linq;

    [TestFixture]
    class APIApprovals
    {
        [Test]
        public void PublicClr()
        {
            var publicApi = ApiGenerator.GeneratePublicApi(typeof(Particular.ServiceControl.Bootstrapper).Assembly);
            Approver.Verify(publicApi);
        }

        [Test]
        public void ServiceControlTransport()
        {
            var serviceControlTrasportApi = ApiGenerator.GeneratePublicApi(typeof(Transports.TransportSettings).Assembly);
            Approver.Verify(serviceControlTrasportApi);
        }

        [Test]
        public void NancyModulePaths()
        {
            var nancyModuleTypes = typeof(BaseModule).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(BaseModule)));

            StaticConfiguration.EnableHeadRouting = true;

            var modules = nancyModuleTypes.Select(m =>
            {
                try
                {
                    return Activator.CreateInstance(m);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }).OrderBy(m => m.GetType().Name).Cast<INancyModule>().ToList();

            var routes = JsonConvert.SerializeObject(modules.ToDictionary(m => m.GetType().FullName, m => m.Routes.Select(r => $"{r.Description.Method}: {r.Description.Path}").OrderBy(r => r)), Formatting.Indented);

            Approver.Verify(routes);
        }

        [Test]
        public void RootPathValue()
        {
            var container = new ServiceContainer();
            container.Register<INancyModule, RootModule>(typeof(RootModule).FullName);
            container.Initialize(r => r.ImplementingType == typeof(RootModule), (factory, instance) => ((RootModule)instance).License = new ActiveLicense
            {
                IsValid = true
            });

            var bootstrapper = new TestBootstrapper(container);

            bootstrapper.Initialise();

            var browser = new Browser(bootstrapper);

            var result = browser.Get("/", with => { with.HostName("localhost"); with.HttpRequest(); });

            Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            Approver.Verify(JToken.Parse(result.Body.AsString()).ToString(Formatting.Indented));
        }

        [Test]
        public void TransportNames()
        {
            //HINT: Those names are used in PowerShell scripts thus constitute a public api
            var transprotNamesType = typeof(ServiceControlInstaller.Engine.Instances.TransportNames);

            var publicTransprotNames = ApiGenerator.GeneratePublicApi(transprotNamesType.Assembly, new []{ transprotNamesType });

            Approver.Verify(publicTransprotNames);
        }
    }
}
