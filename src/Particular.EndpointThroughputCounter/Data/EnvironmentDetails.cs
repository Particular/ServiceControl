class EnvironmentDetails
{
    public string MessageTransport { get; init; }
    public string ReportMethod { get; init; }
    public string[] QueueNames { get; init; }
    public string Prefix { get; init; }
    public string[] IgnoredQueues { get; init; }
    public bool SkipEndpointListCheck { get; init; }
    public bool QueuesAreEndpoints { get; init; }
}