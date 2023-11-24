namespace ServiceControl.AcceptanceTesting.EndpointTemplates
{
    using System.Threading.Tasks;
    using InfrastructureConfig;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;

    public static class ConfigureExtensions
    {
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
    }
}