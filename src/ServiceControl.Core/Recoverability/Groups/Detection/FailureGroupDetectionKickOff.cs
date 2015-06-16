namespace ServiceControl.Recoverability.Groups.Detection
{
    using NServiceBus;

    public class FailureGroupDetectionKickOff : IWantToRunWhenBusStartsAndStops
    {
        public IBus Bus { get; set; }

        public void Start()
        {
            Bus.SendLocal(new StartFailureGroupDetection());
        }

        public void Stop()
        {
        }
    }
}