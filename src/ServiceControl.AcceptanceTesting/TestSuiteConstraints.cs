namespace ServiceControl.AcceptanceTesting
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using EndpointTemplates;
    using InfrastructureConfig;

    public interface ITestSuiteConstraints
    {
        bool SupportsDtc { get; }

        bool SupportsCrossQueueTransactions { get; }

        bool SupportsNativePubSub { get; }

        bool SupportsNativeDeferral { get; }

        bool SupportsOutbox { get; }

        IConfigureEndpointTestExecution CreateTransportConfiguration();

        IConfigureEndpointTestExecution CreatePersistenceConfiguration();
    }

    public class TestSuiteConstraints : ITestSuiteConstraints
    {
        public bool SupportsDtc => false;
        public bool SupportsCrossQueueTransactions => true;
        public bool SupportsNativePubSub => true;
        public bool SupportsNativeDeferral => true;
        public bool SupportsOutbox => false;

        public static TestSuiteConstraints Current = new TestSuiteConstraints();

        public IConfigureEndpointTestExecution CreateTransportConfiguration() =>
            GetTransportIntegrationFromConnectionFile()
            ?? GetTransportIntegrationFromEnvironmentVar()
            ?? new ConfigureEndpointLearningTransport();

        public IConfigureEndpointTestExecution CreatePersistenceConfiguration() => new ConfigureEndpointInMemoryPersistence();

        public DataStoreConfiguration CreateDataStoreConfiguration()
        {
            return new DataStoreConfiguration
            {
                ConnectionString = Environment.GetEnvironmentVariable("ServiceControl/SqlStorageConnectionString"),
                DataStoreTypeName = Environment.GetEnvironmentVariable("ServiceControl/DataStoreType") ?? "RavenDb"
            };
        }

        static ITransportIntegration GetTransportIntegrationFromEnvironmentVar() => CreateTransportIntegration(
            Environment.GetEnvironmentVariable("ServiceControl.AcceptanceTests.TransportCustomization"),
            Environment.GetEnvironmentVariable("ServiceControl.AcceptanceTests.ConnectionString")
        );

        static ITransportIntegration GetTransportIntegrationFromConnectionFile()
        {
            var connectionFile = GetConnectionFile();
            if (connectionFile == null || !File.Exists(connectionFile))
            {
                return null;
            }

            var lines = File.ReadLines(connectionFile)
                .Skip(1) // Name
                .Take(2) // TransportCustomizationTypeName, ConnectionString
                .ToArray();

            return CreateTransportIntegration(lines[0], lines[1]);
        }

        static string GetConnectionFile()
        {
            var directory = AppDomain.CurrentDomain.BaseDirectory;
            while (directory != null)
            {
                if (Directory.EnumerateFiles(directory).Any(file => file.EndsWith(".sln")))
                {
                    return Path.Combine(directory, "connection.txt");
                }

                var parent = Directory.GetParent(directory);

                directory = parent?.FullName;
            }

            return null;
        }

        static ITransportIntegration CreateTransportIntegration(string transportCustomizationToUseString, string connectionString)
        {
            if (string.IsNullOrWhiteSpace(transportCustomizationToUseString))
            {
                return null;
            }

            var customizationTypeName = $"{typeof(ConfigureEndpointLearningTransport).Namespace}.{transportCustomizationToUseString}";

            var customizationType = Assembly.GetExecutingAssembly().GetType(customizationTypeName);
            var transportToUse = (ITransportIntegration)Activator.CreateInstance(customizationType);
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                transportToUse.ConnectionString = connectionString;
            }

            return transportToUse;
        }
    }
}