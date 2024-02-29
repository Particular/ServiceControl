namespace Particular.ThroughputCollector.Contracts
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public class BrokerSettings
    {
        public Broker Broker { get; set; }
        public string IsKnownEndpoint { get; set; }
        public bool? UserIndicatedSendOnly { get; set; }
        public bool? UserIndicatedToIgnore { get; set; }
        public long? MaxDailyThroughputForThisMonth { get; set; }
    }
}
