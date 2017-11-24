using System;

namespace ServiceControl.MessageRedirects
{
    using System.Collections.Concurrent;
    using ServiceControl.Infrastructure;

    public class MessageRedirect
    {
        static ConcurrentDictionary<string, Guid> idCache = new ConcurrentDictionary<string, Guid>();

        public Guid MessageRedirectId
        {
            get
            {
                return idCache.GetOrAdd(FromPhysicalAddress, DeterministicGuid.MakeId);
            }
        }

        public string FromPhysicalAddress { get; set; }
        public string ToPhysicalAddress { get; set; }
        public long LastModifiedTicks { get; set; }
    }
}
