namespace ServiceControl.Persistence;

public class ConnectedApplication
{
    public string Name { get; set; }
    public bool SupportsHeartbeats { get; set; }

    public const string CollectionName = "ConnectedApplications";
}