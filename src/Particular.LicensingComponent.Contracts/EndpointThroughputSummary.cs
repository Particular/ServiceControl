namespace Particular.LicensingComponent.Contracts;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

public class EndpointThroughputSummary
{
    public string Name { get; set; }
    public string NameHash { get; set; }
    public bool IsKnownEndpoint { get; set; }
    public string UserIndicator { get; set; }
    public long MaxDailyThroughput { get; set; }
    public MonthlyThroughput[] MonthlyThroughput { get; set; }
    public long AverageMonthlyThroughput { get; set; }
}

public class UpdateUserIndicator
{
    public string Name { get; set; }
    public string UserIndicator { get; set; }
}

public record MonthlyThroughput(string Month, long Throughput);