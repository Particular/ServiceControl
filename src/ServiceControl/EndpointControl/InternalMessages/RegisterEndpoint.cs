namespace ServiceControl.EndpointControl.InternalMessages
{
    using Contracts.Operations;
    using NServiceBus;

    public class RegisterEndpoint:ICommand
    {
        public EndpointDetails Endpoint { get; set; }
    }
}