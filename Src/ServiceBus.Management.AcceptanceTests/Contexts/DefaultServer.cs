namespace ServiceBus.Management.AcceptanceTests.Contexts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Config.ConfigurationSource;
    using NServiceBus.Features;
    using NServiceBus.Hosting.Helpers;
    using NServiceBus;

    public class DefaultServer : IEndpointSetupTemplate
    {

        public Configure GetConfiguration(RunDescriptor runDescriptor, EndpointConfiguration endpointConfiguration, IConfigurationSource configSource)
        {
            var settings = runDescriptor.Settings;

            var types = GetTypesToUse(endpointConfiguration);

            var transportToUse = settings.GetOrNull("Transport");

            Configure.Features.Enable<Sagas>()
                ; var config = Configure.With(types)
                                  .DefineEndpointName(endpointConfiguration.EndpointName)
                                  .DefineBuilder(settings.GetOrNull("Builder"))
                                  .CustomConfigurationSource(configSource)
                                  .DefineSerializer(settings.GetOrNull("Serializer"))
                                  .DefineTransport(transportToUse)
                                  .InMemorySagaPersister();
                            

            if (transportToUse == null || transportToUse.Contains("Msmq") || transportToUse.Contains("SqlServer") || transportToUse.Contains("RabbitMq"))
                config.UseInMemoryTimeoutPersister();

            if (transportToUse == null || transportToUse.Contains("Msmq") || transportToUse.Contains("SqlServer"))
                config.InMemorySubscriptionStorage();

            config.InMemorySagaPersister();

            return config.UnicastBus();
        }

        static IEnumerable<Type> GetTypesToUse(EndpointConfiguration endpointConfiguration)
        {
            var assemblies = AssemblyScanner.GetScannableAssemblies().Assemblies
                                            .Where(a =>a != typeof (Message).Assembly).ToList(); 

           
            var types = assemblies
                                 .SelectMany(a => a.GetTypes())
                                 .Where(
                                     t =>
                                     t.Assembly != Assembly.GetExecutingAssembly() || //exlude all test types by default
                                     t.DeclaringType == endpointConfiguration.BuilderType.DeclaringType || //but include types on the test level
                                     t.DeclaringType == endpointConfiguration.BuilderType); //and the specific types for this endpoint
            return types;

        }
    }
}