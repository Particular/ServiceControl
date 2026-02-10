namespace ServiceControl.Persistence.Sql.Core.Entities;

public class KnownEndpointEntity
{
    public Guid Id { get; set; }
    public string EndpointName { get; set; } = null!;
    public Guid HostId { get; set; }
    public string Host { get; set; } = null!;
    public string HostDisplayName { get; set; } = null!;
    public bool Monitored { get; set; }
}
