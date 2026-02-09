namespace ServiceControl.Persistence.Sql.Core.Entities;

using System;

public class FailedErrorImportEntity
{
    public Guid Id { get; set; }
    public string MessageJson { get; set; } = null!; // FailedTransportMessage as JSON
    public string? ExceptionInfo { get; set; }
}
