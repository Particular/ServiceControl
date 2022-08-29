namespace ServiceControl.UnitTests.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Reflection;
    using System.Web.Http.Controllers;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using NUnit.Framework;
    using Particular.ServiceControl;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Infrastructure.WebApi;
    using ServiceControl.Persistence;

    [TestFixture]
    class WebApiHostBuilderExtensionsTests
    {
        static IHostBuilder PrepareHostBuilder() => new HostBuilder()
            .ConfigureServices(serviceCollection =>
            {
                serviceCollection.AddSingleton<IDocumentStore>(_ => InMemoryStoreBuilder.GetInMemoryStore());
                serviceCollection.AddSingleton<Func<HttpClient>>(() => new HttpClient());
                serviceCollection.AddSingleton<IDomainEvents, DomainEvents>();
                serviceCollection.AddServiceControlPersistence(DataStoreType.InMemory);
                serviceCollection.AddSingleton(new LoggingSettings("test"));
            })
            .UseNServiceBus(_ =>
            {
                var config = new EndpointConfiguration("test");
                config.UseTransport<LearningTransport>();
                return config;
            })
            .UseServiceControlComponents(
                new ServiceBus.Management.Infrastructure.Settings.Settings(),
                ServiceControlMainInstance.Components);

        [Test]
        public void UseWebApi_RegistersAllControllers()
        {
            // Arrange
            var assembly = Assembly.GetAssembly(typeof(WebApiHostBuilderExtensions));
            var controllerTypes = assembly.DefinedTypes
                .Where(type => typeof(IHttpController).IsAssignableFrom(type) &&
                               type.Name.EndsWith("Controller", StringComparison.Ordinal));

            var hostBuilder = PrepareHostBuilder();

            // Act
            var host = hostBuilder
                .UseWebApi(new List<Assembly> { assembly }, string.Empty, false)
                .Build();

            // Assert
            Assert.That(host, Is.Not.Null);

            foreach (var controllerType in controllerTypes)
            {
                Assert.That(host.Services.GetService(controllerType), Is.Not.Null);
            }
        }
    }
}