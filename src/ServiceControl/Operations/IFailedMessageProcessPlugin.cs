namespace ServiceControl.Operations
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus.Transport;

    interface IErrorMessageBatchPlugin
    {
        Task AfterProcessing(List<MessageContext> batch);
    }
}