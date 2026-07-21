namespace ServiceControl.Persistence.EFCore.Entities;

public class KnownEndpointEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }

    public Guid HostId { get; set; }

    public required string Host { get; set; }

    public bool Monitored { get; set; }
}
