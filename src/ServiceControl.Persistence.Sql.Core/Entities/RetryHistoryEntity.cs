namespace ServiceControl.Persistence.Sql.Core.Entities;

public class RetryHistoryEntity
{
    public int Id { get; set; } = 1; // Singleton pattern
    public string? HistoricOperationsJson { get; set; }
    public string? UnacknowledgedOperationsJson { get; set; }
}
