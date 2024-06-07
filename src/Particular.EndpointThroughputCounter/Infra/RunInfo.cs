namespace Particular.EndpointThroughputCounter.Infra;

using Mindscape.Raygun4Net;

public static class RunInfo
{
    private static readonly Dictionary<string, string> runValues = [];

    public static readonly string TicketId;

    static RunInfo()
    {
        TicketId = Guid.NewGuid().ToString()[..8];
        runValues.Add("TicketId", TicketId);
    }

    public static void Add(string key, string value) => runValues[key] = value;

    public static IRaygunMessageBuilder AddCurrentRunInfo(this IRaygunMessageBuilder builder) => builder.SetUserCustomData(runValues);
}