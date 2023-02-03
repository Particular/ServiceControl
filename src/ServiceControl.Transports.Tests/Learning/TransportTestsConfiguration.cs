namespace ServiceControl.Transport.Tests
{
    using System.IO;
    using System;
    using System.Threading.Tasks;
    using ServiceControl.Transports;
    using ServiceControl.Transports.Learning;
    using System.Linq;

    partial class TransportTestsConfiguration
    {
        public Task Configure()
        {
            customizations = new LearningTransportCustomization();

            basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".transporttests");

            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            return Task.CompletedTask;
        }

        public IProvideQueueLength InitializeQueueLengthProvider(string queueName, Action<QueueLengthEntry> onQueueLengthReported)
        {
            var queueLengthProvider = customizations.CreateQueueLengthProvider();

            queueLengthProvider.Initialize(basePath, (qle, _) => onQueueLengthReported(qle.First()));

            var queuePath = Path.Combine(basePath, queueName);

            if (!Directory.Exists(queuePath))
            {
                Directory.CreateDirectory(queuePath);
            }

            return queueLengthProvider;
        }

        public Task Cleanup() => Task.CompletedTask;

        LearningTransportCustomization customizations;
        string basePath;
    }
}