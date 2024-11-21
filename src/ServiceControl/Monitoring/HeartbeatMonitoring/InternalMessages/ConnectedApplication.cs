namespace ServiceControl.Monitoring;

using NServiceBus;

public class ConnectedApplication : IMessage
{
    public string Application { get; set; }
    public bool SupportsHeartbeats { get; set; }
    public string[] ErrorQueues { get; set; }
}
