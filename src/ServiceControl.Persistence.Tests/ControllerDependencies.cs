namespace ServiceControl.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading.Tasks;
    using System.Web.Http.Controllers;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using NUnit.Framework;
    using Particular.ServiceControl;
    using PersistenceTests;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Infrastructure.WebApi;

    [TestFixture]
    class ControllerDependencies
    {
        /// <summary>
        /// This test makes sure that each persistence has registered all the required services to
        /// instantiate each of the WebAPI controllers present in the ServiceControl app.
        /// </summary>
        [Test]
        public async Task EnsurePersistenceProvidesAllControllerDependencies()
        {
            // Arrange
            var testPersistence = new TestPersistenceImpl();

            var assembly = Assembly.GetAssembly(typeof(WebApiHostBuilderExtensions));
            var controllerTypes = assembly.DefinedTypes
                .Where(type => typeof(IHttpController).IsAssignableFrom(type) &&
                               type.Name.EndsWith("Controller", StringComparison.Ordinal))
                .ToArray();

            var hostBuilder = new HostBuilder()
                .ConfigureServices(serviceCollection =>
                {
                    serviceCollection.AddSingleton<Func<HttpClient>>(() => new HttpClient());
                    serviceCollection.AddSingleton<IDomainEvents, DomainEvents>();
                    serviceCollection.AddSingleton(new LoggingSettings("test"));

                    testPersistence.Configure(serviceCollection);
                })
                .UseNServiceBus(_ =>
                {
                    var config = new EndpointConfiguration("test");
                    config.UseTransport<LearningTransport>().StorageDirectory(Path.Combine(TestContext.CurrentContext.WorkDirectory, "DependencyTest"));
                    return config;
                })
                .ConfigureServices(serviceCollection =>
                {
                    // This test never starts the ServiceControl instance, that means (with NServiceBus v8) that the transport is never initialized.
                    // Some of the controllers, though, access through other dependencies the raw endpoint which tries to configure NServiceBus
                    // The NServiceBus ReceiveComponent fails because the transport is not started and the ReceiveAddress is not properly registered
                    // The component registration must happen AFTER UseNServiceBus because we want to override components registered by NServiceBus
                    serviceCollection.AddSingleton(new ReceiveAddresses("mainReceiveAddress"));
                })
                .UseServiceControlComponents(new Settings(), ServiceControlMainInstance.Components);

            // Act
            using (var host = hostBuilder
                       .UseWebApi([assembly], string.Empty, false)
                       .Build())
            {
                await host.Services.GetRequiredService<IPersistenceLifecycle>().Initialize();

                // Assert
                Assert.That(host, Is.Not.Null);

                // Make sure the list isn't suddenly empty
                Assert.That(controllerTypes.Length, Is.GreaterThan(10));
                foreach (var controllerType in controllerTypes)
                {
                    Console.WriteLine($"Getting service {controllerType.FullName}");
                    Assert.That(host.Services.GetService(controllerType), Is.Not.Null);
                }
            }
        }
    }
}