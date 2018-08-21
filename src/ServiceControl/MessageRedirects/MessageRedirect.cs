namespace ServiceControl.MessageRedirects
{
    using System;
    using System.Collections.Concurrent;
    using Infrastructure;

    public class MessageRedirect
    {
        public Guid MessageRedirectId
        {
            get { return idCache.GetOrAdd(FromPhysicalAddress, DeterministicGuid.MakeId); }
        }

        public string FromPhysicalAddress { get; set; }
        public string ToPhysicalAddress { get; set; }
        public long LastModifiedTicks { get; set; }
        static ConcurrentDictionary<string, Guid> idCache = new ConcurrentDictionary<string, Guid>();
    }
}