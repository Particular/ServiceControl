using System;

namespace ServiceControl.MessageRedirects.InternalMessages
{
    public class EndMessageRedirect
    {
        public Guid MessageRedirectId { get; set; }

        public DateTime ExpiresDateTime { get; set; }
    }
}
