namespace ServiceControl.EndpointPlugin.Infrastructure.ServiceControlBackend
{
    using System;
    using NServiceBus;

    public interface IServiceControlBackend
    {
        void Send(object messageToSend);
        void Send(object messageToSend, TimeSpan timeToBeReceived);
        Address Address { get; }
    }
}
