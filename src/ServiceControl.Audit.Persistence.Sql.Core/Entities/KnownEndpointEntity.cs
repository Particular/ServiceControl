namespace ServiceControl.Audit.Persistence.Sql.Core.Entities;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class KnownEndpointEntity
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public Guid Id { get; set; }
    public string? Name { get; set; }

    public Guid HostId { get; set; }

    public string? Host { get; set; }

    public DateTime LastSeen { get; set; }
}
