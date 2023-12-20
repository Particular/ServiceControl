namespace ServiceControl.Persistence.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.DependencyInjection;
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
                .Where(type => typeof(ControllerBase).IsAssignableFrom(type) &&
                               type.Name.EndsWith("Controller", StringComparison.Ordinal))
                .ToArray();

            var hostBuilder = WebApplication.CreateBuilder();
            hostBuilder.Services.AddHttpClient();
            hostBuilder.Services.AddHttpContextAccessor();
            hostBuilder.Services.AddSingleton<IDomainEvents, DomainEvents>();
            hostBuilder.Services.AddSingleton(new LoggingSettings("test"));
            testPersistence.Configure(hostBuilder.Services);
            hostBuilder.Host.UseNServiceBus(_ =>
            {
                var config = new EndpointConfiguration("test");
                config.UseTransport<LearningTransport>()
                    .StorageDirectory(Path.Combine(TestContext.CurrentContext.WorkDirectory, "DependencyTest"));
                return config;
            });
            // This test never starts the ServiceControl instance, that means (with NServiceBus v8) that the transport is never initialized.
            // Some of the controllers, though, access through other dependencies the raw endpoint which tries to configure NServiceBus
            // The NServiceBus ReceiveComponent fails because the transport is not started and the ReceiveAddress is not properly registered
            // The component registration must happen AFTER UseNServiceBus because we want to override components registered by NServiceBus
            hostBuilder.Services.AddSingleton(new ReceiveAddresses("mainReceiveAddress"));
            hostBuilder.UseServiceControlComponents(new Settings(errorRetentionPeriod: TimeSpan.FromDays(7), forwardErrorMessages: false), ServiceControlMainInstance.Components);
            hostBuilder.UseWebApi([assembly], string.Empty, false);

            // Act
            await using var host = hostBuilder.Build();
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