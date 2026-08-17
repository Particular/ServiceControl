namespace ServiceControl.Persistence.MessageRedirects
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public static class MessageRedirectExtensions
    {
        public static MessageRedirect FindByAddress(this IEnumerable<MessageRedirect> redirects, string fromPhysicalAddress) =>
            redirects.SingleOrDefault(redirect => redirect.FromPhysicalAddress == fromPhysicalAddress);

        public static MessageRedirect FindById(this IEnumerable<MessageRedirect> redirects, Guid messageRedirectId) =>
            redirects.SingleOrDefault(redirect => redirect.MessageRedirectId == messageRedirectId);
    }
}
