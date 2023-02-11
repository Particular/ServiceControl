namespace ServiceControl.Audit.Infrastructure
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Raw;
    using NServiceBus.Transport;

    public interface IQueueIngestorFactory
    {
        Task<IQueueIngestor> InitializeIngestor(string queueName,
            Func<MessageContext, Task> onMessage,
            IErrorHandlingPolicy onError,
            Func<string, Exception, Task> onCriticalError);
    }

    public interface IQueueIngestor
    {
        Task Start();
        Task Stop();
    }
}