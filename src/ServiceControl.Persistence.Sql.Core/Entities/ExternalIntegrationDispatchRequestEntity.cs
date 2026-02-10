namespace ServiceControl.Persistence.Sql.Core.Entities;

using System;

public class ExternalIntegrationDispatchRequestEntity
{
    public long Id { get; set; }
    public string DispatchContextJson { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
