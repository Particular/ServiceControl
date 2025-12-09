namespace ServiceControl.AcceptanceTesting
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using InfrastructureConfig;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;
    using NServiceBus.Hosting.Helpers;

    public static class EndpointConfigurationExtensions
    {
        // This logic has been adapted from Cores ScanTypesForTest to specialize it to the needs of ServiceControl
        public static void ScanTypesForTest(this EndpointConfiguration config,
            EndpointCustomizationConfiguration customizationConfiguration)
        {
            // disable file system scanning for better performance
            // note that this might cause issues when required assemblies are only being loaded at endpoint startup time
            var assemblyScanner = new AssemblyScanner { ScanFileSystemAssemblies = false };

            config.TypesToIncludeInScan(
                assemblyScanner.GetScannableAssemblies().Assemblies
                    .Where(a => !a.FullName!.StartsWith("ServiceControl")) // this prevents handlers, custom checks etc from ServiceControl to be scanned
                    .Where(a => a != customizationConfiguration.BuilderType.Assembly) // exclude all types from test assembly by default
                    .SelectMany(a => a.GetTypes())
                    .Union(GetNestedTypeRecursive(customizationConfiguration.BuilderType.DeclaringType, customizationConfiguration.BuilderType))
                    .Union(customizationConfiguration.TypesToInclude)
                    .ToList());
            return;

            IEnumerable<Type> GetNestedTypeRecursive(Type rootType, Type builderType)
            {
                if (rootType == null)
                {
                    throw new InvalidOperationException("Make sure you nest the endpoint infrastructure inside the TestFixture as nested classes");
                }

                yield return rootType;

                if (typeof(IEndpointConfigurationFactory).IsAssignableFrom(rootType) && rootType != builderType)
                {
                    yield break;
                }

                foreach (var nestedType in rootType.GetNestedTypes(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).SelectMany(t => GetNestedTypeRecursive(t, builderType)))
                {
                    yield return nestedType;
                }
            }
        }

        public static async Task DefinePersistence(this EndpointConfiguration config, RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointCustomizationConfiguration)
        {
            var persistenceConfiguration = new ConfigureEndpointInMemoryPersistence();
            await persistenceConfiguration.Configure(endpointCustomizationConfiguration.EndpointName, config, runDescriptor.Settings, endpointCustomizationConfiguration.PublisherMetadata);
            runDescriptor.OnTestCompleted(_ => persistenceConfiguration.Cleanup());
        }

        public static void RegisterComponentsAndInheritanceHierarchy(this EndpointConfiguration builder, RunDescriptor runDescriptor) => builder.RegisterComponents(services => { RegisterInheritanceHierarchyOfContextOnContainer(runDescriptor, services); });

        static void RegisterInheritanceHierarchyOfContextOnContainer(RunDescriptor runDescriptor,
            IServiceCollection services)
        {
            var type = runDescriptor.ScenarioContext.GetType();
            while (type != typeof(object))
            {
                services.AddSingleton(type, runDescriptor.ScenarioContext);
                type = type.BaseType;
            }
        }

        public static void NoImmediateRetries(this EndpointConfiguration configuration)
            => configuration.Recoverability().Immediate(x => x.NumberOfRetries(0));

        public static void NoDelayedRetries(this EndpointConfiguration configuration)
            => configuration.Recoverability().Delayed(x => x.NumberOfRetries(0));

        public static void NoRetries(this EndpointConfiguration configuration)
        {
            configuration.NoDelayedRetries();
            configuration.NoImmediateRetries();
        }

        public static void NoOutbox(this EndpointConfiguration configuration) => configuration.DisableFeature<Outbox>();

        public static RoutingSettings ConfigureRouting(this EndpointConfiguration configuration) =>
            new(configuration.GetSettings());
    }
}