using System;

namespace ServiceControl.MessageRedirects.InternalMessages
{
    using NServiceBus;

    public class RemoveMessageRedirect : ICommand
    {
        public Guid MessageRedirectId { get; set; }
    }
}
