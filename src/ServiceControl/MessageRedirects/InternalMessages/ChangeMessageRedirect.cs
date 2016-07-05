namespace ServiceControl.MessageRedirects.InternalMessages
{
    using System;
    using NServiceBus;

    public class ChangeMessageRedirect : ICommand
    {
        public string ToPhysicalAddress { get; set; }
        public Guid MessageRedirectId { get; set; }
    }
}
