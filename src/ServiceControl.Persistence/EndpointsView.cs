namespace ServiceControl.Persistence
{
    using System;
    using System.Text.Json.Serialization;

    public class EndpointsView
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string HostDisplayName { get; set; }
        public bool Monitored { get; set; }
        public bool MonitorHeartbeat { get; set; }
        public HeartbeatInformation HeartbeatInformation { get; set; }
        public bool IsSendingHeartbeats { get; set; }
        [JsonIgnore] public bool IsNotSendingHeartbeats { get; set; }
    }
}