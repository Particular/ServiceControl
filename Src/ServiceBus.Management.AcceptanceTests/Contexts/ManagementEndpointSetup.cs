namespace ServiceBus.Management.AcceptanceTests.Contexts
{
    using System.Reflection;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Config.ConfigurationSource;

    public class ManagementEndpointSetup : IEndpointSetupTemplate
    {
        public Configure GetConfiguration(RunDescriptor runDescriptor, EndpointBehavior endpointBehavior,
                                          IConfigurationSource configSource)
        {

            return Configure.With(AllAssemblies.Except(Assembly.GetExecutingAssembly().FullName))
                            .DefaultBuilder()
                            .XmlSerializer()
                            .UseTransport<Msmq>()
                            .UnicastBus();

        }
    }
}