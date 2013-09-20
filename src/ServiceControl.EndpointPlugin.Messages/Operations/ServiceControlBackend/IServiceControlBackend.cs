namespace ServiceControl.EndpointPlugin.Messages.Operations.ServiceControlBackend
{
    using System;

    public interface IServiceControlBackend
    {
        void Send(object messageToSend);
        void Send(object messageToSend, TimeSpan timeToBeReceived);
    }
}
