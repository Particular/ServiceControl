namespace ServiceControl.AcceptanceTesting.EndpointTemplates
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;
    using ServiceControl.AcceptanceTesting.InfrastructureConfig;

    public abstract class DefaultServerBase(IConfigureEndpointTestExecution endpointTestExecutionConfiguration)
        : IEndpointSetupTemplate
    {
        protected DefaultServerBase() : this(new ConfigureEndpointLearningTransport())
        {
        }

        public virtual async Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointCustomizations, Func<EndpointConfiguration, Task> configurationBuilderCustomization)
        {
            var endpointConfiguration = new EndpointConfiguration(endpointCustomizations.EndpointName);

            endpointConfiguration.Pipeline.Register(new StampDispatchBehavior(runDescriptor.ScenarioContext), "Stamps outgoing messages with session ID");
            endpointConfiguration.Pipeline.Register(new DiscardMessagesBehavior(runDescriptor.ScenarioContext), "Discards messages based on session ID");

            endpointConfiguration.SendFailedMessagesTo("error");

            endpointConfiguration.EnableInstallers();

            await endpointTestExecutionConfiguration.Configure(endpointCustomizations.EndpointName, endpointConfiguration, runDescriptor.Settings, endpointCustomizations.PublisherMetadata);
            runDescriptor.OnTestCompleted(_ => endpointTestExecutionConfiguration.Cleanup());

            endpointConfiguration.RegisterComponentsAndInheritanceHierarchy(runDescriptor);
            await endpointConfiguration.DefinePersistence(runDescriptor, endpointCustomizations);

            endpointConfiguration.UseSerialization<SystemJsonSerializer>();

            endpointConfiguration.Pipeline.Register<TraceIncomingBehavior.Registration>();
            endpointConfiguration.Pipeline.Register<TraceOutgoingBehavior.Registration>();

            endpointConfiguration.Conventions().DefiningEventsAs(t => typeof(IEvent).IsAssignableFrom(t) || IsExternalContract(t));

            endpointConfiguration.GetSettings().Set("SC.ScenarioContext", runDescriptor.ScenarioContext);

            endpointConfiguration.DisableFeature<AutoSubscribe>();

            await configurationBuilderCustomization(endpointConfiguration);

            // scan types at the end so that all types used by the configuration have been loaded into the AppDomain
            endpointConfiguration.ScanTypesForTest(endpointCustomizations);

            return endpointConfiguration;
        }

        static bool IsExternalContract(Type t) =>
            t.Namespace != null && t.Namespace.StartsWith("ServiceControl.Contracts")
                                && t.Assembly.GetName().Name == "ServiceControl.Contracts";
    }
}