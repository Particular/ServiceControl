namespace ServiceControl.Operations
{
    using System.Collections.Generic;
    using NServiceBus.Transport;
    using Raven.Abstractions.Commands;

    interface IErrorMessageBatchPlugin
    {
        void AfterProcessing(List<MessageContext> batch, ICollection<ICommandData> commands);
    }
}