namespace ServiceControl.Persistence;

public class EndpointSettings
{
    public string Name { get; set; }
    public bool TrackInstances { get; set; }

    public const string CollectionName = "EndpointSettings";
}