namespace ServiceControl.MessageFailures.InternalMessages
{
    using System;
    using NServiceBus;

    [Obsolete("Only used by legacy RavenDB35 storage engine")] // TODO: how to deal with this domain event
    class ReclassifyErrors : ICommand
    {
        public bool Force { get; set; }
    }
}