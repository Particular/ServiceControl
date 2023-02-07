namespace ServiceControl.Transport.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Raw;
    using ServiceControl.Transports;
    using ServiceControl.Transports.Learning;

    partial class TransportTestsConfiguration
    {
        public Task Configure()
        {
            customizations = new LearningTransportCustomization();

            basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".transporttests");

            if (Directory.Exists(basePath))
            {
                Directory.Delete(basePath, true);
            }

            Directory.CreateDirectory(basePath);

            return Task.CompletedTask;
        }

        public IProvideQueueLength InitializeQueueLengthProvider(Action<QueueLengthEntry> onQueueLengthReported)
        {
            var queueLengthProvider = customizations.CreateQueueLengthProvider();

            queueLengthProvider.Initialize(basePath, (qle, _) => onQueueLengthReported(qle.First()));

            return queueLengthProvider;
        }

        public void ApplyTransportConfig(RawEndpointConfiguration c)
        {
            c.UseTransport<LearningTransport>()
                .StorageDirectory(basePath);
        }

        public Task Cleanup() => Task.CompletedTask;

        LearningTransportCustomization customizations;
        string basePath;
    }
}