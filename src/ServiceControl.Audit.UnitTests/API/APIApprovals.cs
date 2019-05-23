namespace ServiceControl.UnitTests.API
{
    using System;
    using System.Linq;
    using Audit.Infrastructure;
    using Audit.Infrastructure.Nancy.Modules;
    using Audit.Infrastructure.Settings;
    using Audit.UnitTests.API;
    using LightInject;
    using Nancy;
    using Nancy.Testing;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
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

            var bootstrapper = new TestBootstrapper(container);

            bootstrapper.Initialise();

            var browser = new Browser(bootstrapper);

            var result = browser.Get("/", with =>
            {
                with.HostName("localhost");
                with.HttpRequest();
            });

            Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            Approver.Verify(JToken.Parse(result.Body.AsString()).ToString(Formatting.Indented));
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