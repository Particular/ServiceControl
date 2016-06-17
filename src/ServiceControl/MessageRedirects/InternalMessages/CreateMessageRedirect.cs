namespace ServiceControl.MessageRedirects.InternalMessages
{
    using System;

    public class CreateMessageRedirect
    {
        public Guid MessageRedirectId { get; set; }
        public string FromPhysicalAddress { get; set; }
        public string ToPhysicalAddress { get; set; }
    }
}