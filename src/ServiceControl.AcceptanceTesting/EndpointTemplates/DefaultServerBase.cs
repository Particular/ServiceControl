namespace ServiceControl.AcceptanceTesting.EndpointTemplates
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;
    using ServiceControl.AcceptanceTesting.InfrastructureConfig;

    public class DefaultServerBase<TBootstrapper> : IEndpointSetupTemplate
    {
        public DefaultServerBase() : this(new ConfigureEndpointLearningTransport())
        {
        }

        public DefaultServerBase(IConfigureEndpointTestExecution endpointTestExecutionConfiguration) => this.endpointTestExecutionConfiguration = endpointTestExecutionConfiguration;

        public async Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointCustomizations, Func<EndpointConfiguration, Task> configurationBuilderCustomization)
        {
            ServicePointManager.DefaultConnectionLimit = 100;

            var endpointConfiguration = new EndpointConfiguration(endpointCustomizations.EndpointName);

            endpointConfiguration.Pipeline.Register(new StampDispatchBehavior(runDescriptor.ScenarioContext), "Stamps outgoing messages with session ID");
            endpointConfiguration.Pipeline.Register(new DiscardMessagesBehavior(runDescriptor.ScenarioContext), "Discards messages based on session ID");

            endpointConfiguration.SendFailedMessagesTo("error");

            endpointConfiguration.EnableInstallers();

            await endpointTestExecutionConfiguration.Configure(endpointCustomizations.EndpointName, endpointConfiguration, runDescriptor.Settings, endpointCustomizations.PublisherMetadata);
            runDescriptor.OnTestCompleted(_ => endpointTestExecutionConfiguration.Cleanup());

            endpointConfiguration.RegisterComponentsAndInheritanceHierarchy(runDescriptor);
            await endpointConfiguration.DefinePersistence(runDescriptor, endpointCustomizations);

            endpointConfiguration.UseSerialization<NewtonsoftJsonSerializer>();

            endpointConfiguration.Pipeline.Register<TraceIncomingBehavior.Registration>();
            endpointConfiguration.Pipeline.Register<TraceOutgoingBehavior.Registration>();

            endpointConfiguration.Conventions().DefiningEventsAs(t => typeof(IEvent).IsAssignableFrom(t) || IsExternalContract(t));

            endpointConfiguration.GetSettings().Set("SC.ScenarioContext", runDescriptor.ScenarioContext);

            endpointConfiguration.DisableFeature<AutoSubscribe>();

            await configurationBuilderCustomization(endpointConfiguration);

            // scan types at the end so that all types used by the configuration have been loaded into the AppDomain
            endpointConfiguration.TypesToIncludeInScan(endpointCustomizations.GetTypesScopedByTestClass<TBootstrapper>().Concat(new[]
            {
                typeof(TraceIncomingBehavior), typeof(TraceOutgoingBehavior)
            }).ToList());

            return endpointConfiguration;
        }

        static bool IsExternalContract(Type t)
        {
            return t.Namespace != null && t.Namespace.StartsWith("ServiceControl.Contracts")
                                       && t.Assembly.GetName().Name == "ServiceControl.Contracts";
        }

        IConfigureEndpointTestExecution endpointTestExecutionConfiguration;
    }
}