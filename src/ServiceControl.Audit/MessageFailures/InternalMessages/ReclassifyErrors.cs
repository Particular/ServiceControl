namespace ServiceControl.MessageFailures.InternalMessages
{
    using NServiceBus;

    class ReclassifyErrors : ICommand
    {
        public bool Force { get; set; }
    }
}