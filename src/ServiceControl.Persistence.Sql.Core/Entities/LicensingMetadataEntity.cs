namespace ServiceControl.Persistence.Sql.Core.Entities;

public class LicensingMetadataEntity
{
    public int Id { get; set; }
    public required string Key { get; set; }
    public required string Data { get; set; }
}