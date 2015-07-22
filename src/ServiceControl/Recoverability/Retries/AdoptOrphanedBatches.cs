namespace ServiceControl.Recoverability
{
    using System;
    using NServiceBus;

    public class AdoptOrphanedBatches : ICommand
    {
        public DateTimeOffset StartupTime { get; set; }
    }
}