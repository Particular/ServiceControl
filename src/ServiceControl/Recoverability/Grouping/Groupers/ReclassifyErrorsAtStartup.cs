namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using ServiceControl.MessageFailures.InternalMessages;

    class ReclassifyErrorsAtStartup : IWantToRunWhenBusStartsAndStops
    {
        readonly IBus bus;

        public ReclassifyErrorsAtStartup(IBus bus)
        {
            this.bus = bus;
        }

        public void Start()
        {
            bus.SendLocal(new ReclassifyErrors());
        }

        public void Stop()
        {
        }
    }
}