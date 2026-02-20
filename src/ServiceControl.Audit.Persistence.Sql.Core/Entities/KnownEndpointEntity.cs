namespace ServiceControl.Audit.Persistence.Sql.Core.Entities;

public class KnownEndpointEntity
{
    public Guid Id { get; set; }
    public string? Name { get; set; }

    public Guid HostId { get; set; }

    public string? Host { get; set; }

    public DateTime LastSeen { get; set; }
}
