namespace ServiceControl.Persistence.Sql.Core.Entities;

public class DailyThroughputEntity
{
    public int Id { get; set; }
    public required string EndpointName { get; set; }
    public required string ThroughputSource { get; set; }
    public required DateOnly Date { get; set; }
    public required long MessageCount { get; set; }
}