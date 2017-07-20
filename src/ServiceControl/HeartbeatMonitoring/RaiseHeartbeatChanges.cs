namespace ServiceControl.HeartbeatMonitoring
{
    using Contracts.HeartbeatMonitoring;
    using NServiceBus;

    public class RaiseHeartbeatChanges :
        IHandleMessages<HeartbeatStatusChanged>
    {
        public IBus Bus { get; set; }

        public HeartbeatStatusProvider StatusProvider { get; set; }

        public void Handle(HeartbeatStatusChanged message)
        {
            PublishUpdate(StatusProvider.GetHeartbeatsStats());
        }

        void PublishUpdate(HeartbeatsStats stats)
        {
            Bus.Publish(new HeartbeatsUpdated
            {
                Active = stats.Active,
                Failing = stats.Dead,
            });
        }
    }
}