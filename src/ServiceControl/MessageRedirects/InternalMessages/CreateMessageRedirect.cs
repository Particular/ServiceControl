namespace ServiceControl.MessageRedirects.InternalMessages
{
    using System;
    using NServiceBus;

    public class CreateMessageRedirect : ICommand
    {
        public Guid MessageRedirectId { get; set; }
        public string FromPhysicalAddress { get; set; }
        public string ToPhysicalAddress { get; set; }
    }
}