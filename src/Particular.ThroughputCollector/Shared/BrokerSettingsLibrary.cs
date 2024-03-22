namespace Particular.ThroughputCollector.Shared;

using Contracts;

public static class BrokerSettingsLibrary
{
    public static List<BrokerSettings> AllBrokerSettings { get; private set; } = [];
    public static string SettingsNamespace = "ThroughputCollector";

    public static BrokerSettings Find(Broker broker) => AllBrokerSettings.First(w => w.Broker == broker);
}


