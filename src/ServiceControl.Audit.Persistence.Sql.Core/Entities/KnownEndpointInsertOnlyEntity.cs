namespace ServiceControl.Audit.Persistence.Sql.Core.Entities;

public class KnownEndpointInsertOnlyEntity
{
    public long Id { get; set; }
    public Guid KnownEndpointId { get; set; }

    public string? Name { get; set; }

    public Guid HostId { get; set; }

    public string? Host { get; set; }

    public DateTime LastSeen { get; set; }
}
