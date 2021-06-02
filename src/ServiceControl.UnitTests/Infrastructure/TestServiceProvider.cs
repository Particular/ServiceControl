namespace ServiceControl.UnitTests.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Autofac;
    using Autofac.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using NServiceBus.Extensibility;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CompositeViews.Messages;

    [TestFixture]
    class TestServiceProvider
    {
        [Test]
        public void VerifyCanResolveEverything()
        {
            var settings = new ServiceBus.Management.Infrastructure.Settings.Settings
            {
                TransportConnectionString = Path.GetTempPath(),
                Components = Components.All
            };
            var endpointConfiguration = new EndpointConfiguration(settings.ServiceName);
            var loggingSettings = new LoggingSettings(settings.ServiceName);
            var bootstrapper = new Bootstrapper(settings, endpointConfiguration, loggingSettings);
            bootstrapper.HostBuilder.ConfigureContainer<ContainerBuilder>(containerBuilder =>
            {
                // HINT: We can't resolve this without creating the endpoint but in this case we can assume NSB will give it to us
                containerBuilder.RegisterInstance<IDispatchMessages>(new FakeMessageDispatcher());
            });
            using (var host = bootstrapper.HostBuilder.Build())
            {
                var scope = host.Services.GetRequiredService<ILifetimeScope>();

                var services = scope.ComponentRegistry.Registrations
                    .SelectMany(x => x.Services)
                    .OfType<IServiceWithType>()
                    .ToList();

                var ignored = new[]
                {
                    typeof(IBuilder),
                    typeof(IDispatchMessages),
                    typeof(IDisposable)
                };

                var notResolved = new List<Type>();
                var hasProperties = new List<Type>();
                foreach (var service in services)
                {
                    if (ignored.Contains(service.ServiceType))
                    {
                        continue;
                    }
                    try
                    {
                        var resolved = scope.Resolve(service.ServiceType);
                        var concreteType = resolved.GetType();

                        // HINT: We only care about types we control
                        if (concreteType.FullName.StartsWith("ServiceControl") == false)
                        {
                            continue;
                        }

                        if (typeof(IApi).IsAssignableFrom(concreteType))
                        {
                            // HINT: API classes DO have properties autowired
                            continue;
                        }

                        var resolvableProperties =
                            concreteType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                .Where(t => scope.IsRegistered(t.PropertyType))
                                .ToArray();

                        if (resolvableProperties.Any())
                        {
                            hasProperties.Add(concreteType);
                            Console.WriteLine($"RESOLVABLE PUBLIC PROPERTIES WILL NOT BE INJECTED. MAY CAUSE RESOLUTION PROBLEMS. {service.ServiceType} - {concreteType}");
                            foreach (var property in resolvableProperties)
                            {
                                Console.WriteLine($"\t{property.Name}");
                            }

                            Console.WriteLine();
                        }

                    }
                    catch (Exception e)
                    {
                        notResolved.Add(service.ServiceType);
                        Console.WriteLine($"COUNT NOT RESOLVE: {service.ServiceType}");
                        Console.WriteLine(e);
                        Console.WriteLine();
                    }
                }

                CollectionAssert.IsEmpty(notResolved, "Everything in the container should be resolvable");

                CollectionAssert.IsEmpty(hasProperties, "Our types should not have public properties (we don't do property injection)");
            }
        }

        class FakeMessageDispatcher : IDispatchMessages
        {
            public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context) => throw new NotImplementedException();
        }
    }
}
