namespace ServiceControl.Persistence.Sql.Core.Entities;

using System;

public class MessageBodyEntity
{
    public Guid Id { get; set; }
    public byte[] Body { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public int BodySize { get; set; }
    public string? Etag { get; set; }
}
