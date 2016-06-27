using System;

namespace ServiceControl.MessageRedirects.InternalMessages
{
    public class RemoveMessageRedirect
    {
        public Guid MessageRedirectId { get; set; }
    }
}
