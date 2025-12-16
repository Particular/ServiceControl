namespace ServiceControl.Persistence.Sql.Core.Entities;

using System;

public class MessageRedirectsEntity
{
    public Guid Id { get; set; }
    public required string ETag { get; set; }
    public DateTime LastModified { get; set; }
    public required string RedirectsJson { get; set; }
}
