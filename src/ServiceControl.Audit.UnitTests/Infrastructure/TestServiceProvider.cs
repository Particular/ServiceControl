namespace ServiceControl.Audit.UnitTests.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Audit.Auditing.MessagesView;
    using Audit.Infrastructure;
    using Audit.Infrastructure.Settings;
    using Autofac;
    using Autofac.Core;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Transport;
    using NUnit.Framework;

    [TestFixture]
    class TestServiceProvider
    {
        [Test]
        public void VerifyCanResolveEverything()
        {
            var settings = new Settings
            {
                TransportConnectionString = Path.GetTempPath()
            };
            var endpointConfiguration = new EndpointConfiguration(settings.ServiceName);
            var loggingSettings = new LoggingSettings(settings.ServiceName);
            var bootstrapper = new Bootstrapper(_ => { }, settings, endpointConfiguration, loggingSettings);
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
                        Console.WriteLine($"COULD NOT RESOLVE: {service.ServiceType}");
                        Console.WriteLine(e);
                        Console.WriteLine();
                    }
                }

                CollectionAssert.IsEmpty(notResolved, "Everything in the container should be resolvable");

                CollectionAssert.IsEmpty(hasProperties, "Our types should not have public properties (we don't do property injection)");
            }
        }
    }
}
