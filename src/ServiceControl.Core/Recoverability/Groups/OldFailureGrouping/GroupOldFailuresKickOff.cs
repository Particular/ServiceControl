namespace ServiceControl.Recoverability.Groups.OldFailureGrouping
{
    using NServiceBus;

    public class GroupOldFailuresKickOff : IWantToRunWhenBusStartsAndStops
    {
        public IBus Bus { get; set; }

        public void Start()
        {
            Bus.SendLocal(new GroupOldFailures());
        }

        public void Stop()
        {
        }
    }
}