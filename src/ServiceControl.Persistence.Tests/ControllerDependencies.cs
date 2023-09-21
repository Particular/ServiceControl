namespace ServiceControl.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
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
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Infrastructure.WebApi;
    using ServiceControl.PersistenceTests;

    [TestFixture]
    class ControllerDependencies
    {
        /// <summary>
        /// This test makes sure that each persistence has registered all the required services to
        /// instantiate each of the WebAPI controllers present in the ServiceControl app.
        /// </summary>
        [Test]
        public Task EnsurePersistenceProvidesAllControllerDependencies()
        {
            // Arrange
            //var testPersistence = null; //TODO: new TestPersistenceImpl();

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

                    //testPersistence.Configure(serviceCollection);
                })
                .UseNServiceBus(_ =>
                {
                    var config = new EndpointConfiguration("test");
                    config.UseTransport<LearningTransport>();
                    return config;
                })
                .UseServiceControlComponents(new Settings(), ServiceControlMainInstance.Components);

            // Act
            var host = hostBuilder
                .UseWebApi(new List<Assembly> { assembly }, string.Empty, false)
                .Build();

            // Assert
            Assert.That(host, Is.Not.Null);

            // TODO: Kind of a hack, but gets the job done, since Raven35/5 have different startup requirements
            // Could come up with a more cohesive way of doing this or just remove the IF when Raven35 goes away
            //if (testPersistence.GetType().Assembly.FullName.Contains("RavenDb5"))
            //{
            //    await host.Services.GetRequiredService<IPersistenceLifecycle>().Initialize();
            //}

            // Make sure the list isn't suddenly empty
            Assert.That(controllerTypes.Length, Is.GreaterThan(10));
            foreach (var controllerType in controllerTypes)
            {
                Console.WriteLine($"Getting service {controllerType.FullName}");
                Assert.That(host.Services.GetService(controllerType), Is.Not.Null);
            }

            return Task.CompletedTask;
        }
    }
}