namespace ServiceControl.Plugin.Heartbeat.Messages
{
    using System.Collections.Generic;

    class HostInformation
    {
        public string HostId { get; set; }
        public Dictionary<string, string> Properties { get; set; } 
    }
}