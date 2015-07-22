namespace ServiceControl.Recoverability.Groups.OldFailureGrouping
{
    using System;
    using NServiceBus;

    public class GroupOldFailuresKickOff : IWantToRunWhenBusStartsAndStops
    {
        public IBus Bus { get; set; }

        public void Start()
        {
            Bus.SendLocal(new CheckForOldFailures());
        }

        public void Stop()
        {
        }
    }
}