namespace ServiceControl.Persistence.RavenDB.MessageRedirects
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ServiceControl.Persistence.MessageRedirects;

    class MessageRedirectsCollection
    {
        public List<StoredRedirect> Redirects { get; set; } = [];

        public IReadOnlyList<MessageRedirect> ToRedirects() =>
            [.. Redirects.Select(redirect => new MessageRedirect
            {
                FromPhysicalAddress = redirect.FromPhysicalAddress,
                ToPhysicalAddress = redirect.ToPhysicalAddress,
                LastModified = new DateTime(redirect.LastModifiedTicks, DateTimeKind.Utc)
            })];

        public void Add(MessageRedirect redirect) => Redirects.Add(new StoredRedirect
        {
            FromPhysicalAddress = redirect.FromPhysicalAddress,
            ToPhysicalAddress = redirect.ToPhysicalAddress,
            LastModifiedTicks = redirect.LastModified.Ticks
        });

        public void Update(MessageRedirect redirect)
        {
            var existing = Redirects.SingleOrDefault(stored => stored.FromPhysicalAddress == redirect.FromPhysicalAddress);

            if (existing == null)
            {
                return;
            }

            existing.ToPhysicalAddress = redirect.ToPhysicalAddress;
            existing.LastModifiedTicks = redirect.LastModified.Ticks;
        }

        public void Remove(MessageRedirect redirect) =>
            Redirects.RemoveAll(stored => stored.FromPhysicalAddress == redirect.FromPhysicalAddress);

        public class StoredRedirect
        {
            public string FromPhysicalAddress { get; set; }
            public string ToPhysicalAddress { get; set; }
            public long LastModifiedTicks { get; set; }
        }
    }
}
