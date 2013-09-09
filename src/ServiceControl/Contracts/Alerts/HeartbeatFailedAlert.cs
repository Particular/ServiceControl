namespace ServiceControl.Contracts.Alerts
{
    using System;
    public class HeartbeatFailedAlert : EndpointAlertRaised
    {
        public DateTime LastHeartbeatReceivedAt { get; set; }
    }
}
