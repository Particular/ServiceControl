namespace ServiceControl.Audit.Persistence.Sql.Core.Abstractions;

public class MinimumRequiredStorageState
{
    public bool CanIngestMore { get; set; } = true;
}
