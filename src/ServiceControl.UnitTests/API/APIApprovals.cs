namespace ServiceControl.UnitTests.API
{
    using LightInject;
    using LightInject.Nancy;
    using Nancy;
    using Nancy.Testing;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using Particular.Approvals;
    using Particular.ServiceControl.Licensing;
    using PublicApiGenerator;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.CompositeViews.Messages;
    using System;
    using System.Linq;
    //using ServiceControl.Infrastructure.DomainEvents;
    //using ServiceControl.Infrastructure.SignalR;
    //using System.Linq;
    using System.Runtime.CompilerServices;

    [TestFixture]
    class APIApprovals
    {
        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void PublicClr()
        {
            var publicApi = ApiGenerator.GeneratePublicApi(typeof(Particular.ServiceControl.Bootstrapper).Assembly);
            Approver.Verify(publicApi);
        }

        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ServiceControlTrasport()
        {
            var serviceControlTrasportApi = ApiGenerator.GeneratePublicApi(typeof(Transports.TransportSettings).Assembly);
            Approver.Verify(serviceControlTrasportApi);
        }

        [Test]
        public void NancyModulePaths()
        {
            var scTypes = typeof(BaseModule).Assembly.GetTypes();

            var excludedModules = new[]
            {
                typeof(RoutedApi<>)
            };

            var nancyModuleTypes = scTypes.Where(t => t.IsSubclassOf(typeof(BaseModule)) && !excludedModules.Contains(t)).ToList();

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

            // When
            var result = browser.Get("/", with => { with.HostName("localhost"); with.HttpRequest(); });

            // Then
            Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            Approver.Verify(JToken.Parse(result.Body.AsString()).ToString(Formatting.Indented));
        }

        class TestBootstrapper : LightInjectNancyBootstrapper
        {
            readonly IServiceContainer container;

            public TestBootstrapper(IServiceContainer container)
            {
                this.container = container;
            }
            protected override IServiceContainer GetServiceContainer()
            {
                return container;
            }
        }
    }
}
