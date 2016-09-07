using System;

namespace ServiceControl.MessageRedirects
{
    using ServiceControl.Infrastructure;

    public class MessageRedirect
    {
        public Guid MessageRedirectId => DeterministicGuid.MakeId(FromPhysicalAddress);
        public string FromPhysicalAddress { get; set; }
        public string ToPhysicalAddress { get; set; }
        public long LastModifiedTicks { get; set; }
    }
}
