namespace ServiceControl.MessageFailures.InternalMessages
{
    using NServiceBus;

    public class ReclassifyErrors : ICommand
    {
        public bool Force { get; set; }
    }
}