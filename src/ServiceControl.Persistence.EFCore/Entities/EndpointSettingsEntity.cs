namespace ServiceControl.Persistence.EFCore.Entities;

public class EndpointSettingsEntity
{
    public required string Name { get; set; }
    public bool TrackInstances { get; set; }
}