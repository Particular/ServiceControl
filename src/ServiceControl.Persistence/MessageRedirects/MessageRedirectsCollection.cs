namespace ServiceControl.Persistence.MessageRedirects
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Legacy stored data")]
    public class MessageRedirectsCollection
    {
        public string ETag { get; set; }

        public DateTime LastModified { get; set; }

        public MessageRedirect this[string from] => Redirects.SingleOrDefault(r => r.FromPhysicalAddress == from);
        public MessageRedirect this[Guid id] => Redirects.SingleOrDefault(r => r.MessageRedirectId == id);

        public List<MessageRedirect> Redirects { get; set; } = [];

        public const string DefaultId = "messageredirects";
    }
}