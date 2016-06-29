namespace ServiceControl.MessageRedirects.InternalMessages
{
    using System;

    public class ChangeMessageRedirect
    {
        public string ToPhysicalAddress { get; set; }
        public Guid MessageRedirectId { get; set; }
    }
}
