namespace ServiceControl.HeartbeatMonitoring.InternalMessages
{
    using System;
    using NServiceBus;

    public class RegisterPotentiallyMissingHeartbeats : ICommand
    {
        public Guid EndpointInstanceId { get; set; }
        public DateTime LastHeartbeatAt { get; set; }
        public DateTime DetectedAt { get; set; }
    }
}