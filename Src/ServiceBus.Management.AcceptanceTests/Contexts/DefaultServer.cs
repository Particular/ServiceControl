namespace ServiceBus.Management.AcceptanceTests.Contexts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Config.ConfigurationSource;
    using NServiceBus.Hosting.Helpers;
    using NServiceBus;

    public class DefaultServer : IEndpointSetupTemplate
    {

        public Configure GetConfiguration(RunDescriptor runDescriptor, EndpointBehavior endpointBehavior, IConfigurationSource configSource)
        {
            var settings = runDescriptor.Settings;

            var types = GetTypesToUse(endpointBehavior);

            var transportToUse = settings.GetOrNull("Transport");

            var config = Configure.With(types)
                            .DefineEndpointName(endpointBehavior.EndpointName)
                            .DefineBuilder(settings.GetOrNull("Builder"))
                            .CustomConfigurationSource(configSource)
                            .DefineSerializer(settings.GetOrNull("Serializer"))
                            .DefineTransport(transportToUse);

            if (transportToUse == null || transportToUse.Contains("Msmq") || transportToUse.Contains("SqlServer") || transportToUse.Contains("RabbitMq"))
                config.UseInMemoryTimeoutPersister();

            if (transportToUse == null || transportToUse.Contains("Msmq") || transportToUse.Contains("SqlServer"))
                config.InMemorySubscriptionStorage();

            config.InMemorySagaPersister();

            return config.UnicastBus();
        }

        static IEnumerable<Type> GetTypesToUse(EndpointBehavior endpointBehavior)
        {
            var assemblies = AssemblyScanner.GetScannableAssemblies().Assemblies
                                            .Where(a =>a != typeof (Message).Assembly).ToList(); 

           
            var types = assemblies
                                 .SelectMany(a => a.GetTypes())
                                 .Where(
                                     t =>
                                     t.Assembly != Assembly.GetExecutingAssembly() || //exlude all test types by default
                                     t.DeclaringType == endpointBehavior.BuilderType.DeclaringType || //but include types on the test level
                                     t.DeclaringType == endpointBehavior.BuilderType); //and the specific types for this endpoint
            return types;

        }
    }
}