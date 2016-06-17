namespace ServiceControl.Contracts.MessageRedirects
{
    using System;

    public class MessageRedirectCreated
    {
        public string MessageRedirectId { get; set; }
        public string FromPhysicalAddress { get; set; }
        public string ToPhysicalAddress { get; set; }
        public DateTime Created { get; set; }
    }
}
