namespace Particular.LicensingComponent.Contracts;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

public class EndpointThroughputSummary
{
    public string Name { get; set; }
    public bool IsKnownEndpoint { get; set; }
    public string UserIndicator { get; set; }
    public long MaxDailyThroughput { get; set; }
    public long MaxMonthlyThroughput { get; set; }
}

public class UpdateUserIndicator
{
    public string Name { get; set; }
    public string UserIndicator { get; set; }
}