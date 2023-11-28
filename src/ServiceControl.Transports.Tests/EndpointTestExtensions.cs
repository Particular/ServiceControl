namespace ServiceControl.Transport.Tests
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;

    public static class EndpointTestExtensions
    {
        public static void RegisterComponentsAndInheritanceHierarchy(this EndpointConfiguration builder, RunDescriptor runDescriptor) => builder.RegisterComponents(
            services => { RegisterInheritanceHierarchyOfContextOnContainer(runDescriptor, services); });

        static void RegisterInheritanceHierarchyOfContextOnContainer(RunDescriptor runDescriptor,
            IServiceCollection services)
        {
            Type type = runDescriptor.ScenarioContext.GetType();
            while (type != typeof(object))
            {
                services.AddSingleton(type, runDescriptor.ScenarioContext);
                type = type.BaseType;
            }
        }
    }
}