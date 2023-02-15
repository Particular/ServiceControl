namespace ServiceControl.Transport.Tests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Raw;
    using ServiceControl.Transports;
    using ServiceControl.Transports.Learning;

    partial class TransportTestsConfiguration
    {
        public Task Configure()
        {
            basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".transporttests");

            if (Directory.Exists(basePath))
            {
                Directory.Delete(basePath, true);
            }

            Directory.CreateDirectory(basePath);

            TransportCustomization = new LearningTransportCustomization();
            ConnectionString = basePath;

            return Task.CompletedTask;
        }

        public string ConnectionString { get; private set; }

        public TransportCustomization TransportCustomization { get; private set; }

        public void ApplyTransportConfig(RawEndpointConfiguration c)
        {
            c.UseTransport<LearningTransport>()
                .StorageDirectory(basePath);
        }

        public Task Cleanup()
        {
            if (Directory.Exists(basePath))
            {
                Directory.Delete(basePath, true);
            }

            return Task.CompletedTask;
        }

        string basePath;
    }
}