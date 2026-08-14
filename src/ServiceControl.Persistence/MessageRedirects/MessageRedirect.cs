namespace ServiceControl.Persistence.MessageRedirects
{
    using System;
    using System.Collections.Concurrent;
    using Infrastructure;

    public class MessageRedirect
    {
        public Guid MessageRedirectId => idCache.GetOrAdd(FromPhysicalAddress, DeterministicGuid.MakeId);

        public required string FromPhysicalAddress { get; set; }
        public required string ToPhysicalAddress { get; set; }
        public DateTime LastModified { get; set; }
        static ConcurrentDictionary<string, Guid> idCache = new ConcurrentDictionary<string, Guid>();
    }
}
