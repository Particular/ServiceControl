namespace Particular.LicensingComponent.Contracts;

public readonly struct EndpointDailyThroughput(DateOnly date, long messageCount)
{
    public DateOnly DateUTC { get; } = date;

    public long MessageCount { get; } = messageCount;

    public void Deconstruct(out DateOnly date, out long messageCount)
    {
        date = DateUTC;
        messageCount = MessageCount;
    }
}