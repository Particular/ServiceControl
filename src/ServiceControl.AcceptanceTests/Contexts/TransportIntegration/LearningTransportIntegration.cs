using System;

namespace ServiceBus.Management.AcceptanceTests.Contexts.TransportIntegration
{
    using System.IO;
    using NServiceBus;

    class LearningTransportIntegration : ITransportIntegration
    {
        private static string UniqueId = Guid.NewGuid().ToString("N");

        public LearningTransportIntegration()
        {
            ConnectionString = Path.Combine(Path.GetTempPath(), UniqueId);
            Directory.CreateDirectory(ConnectionString);
        }

        public string Name => "Learning";
        public Type Type => typeof(LearningTransport);
        public string TypeName => "NServiceBus.LearningTransport, ServiceControl.LearningTransport";
        public string ConnectionString { get; set; }

        public void OnEndpointShutdown(string endpointName)
        {
        }

        public void TearDown()
        {
            Directory.Delete(ConnectionString, true);

            Console.WriteLine($"Learning Transport working directory deleted: {ConnectionString}");
        }

        public void Setup()
        {
            if (Directory.Exists(ConnectionString))
            {
                Directory.Delete(ConnectionString);
            }

            Directory.CreateDirectory(ConnectionString);

            Console.WriteLine($"Running Learning Transport at {ConnectionString}");
        }
    }
}
