namespace ServiceBus.Management.Operations.Heartbeats
{
    public class HeartbeatSummary
    {
        public int ActiveEndpoints { get; set; }

        public int NumberOfFailingEndpoints { get; set; }
    }
}