namespace Particular.ThroughputCollector.Infrastructure;

using System.Collections.Frozen;
using Contracts;
using ServiceControl.Configuration;
using ServiceControl.Transports;

public class ThroughputSettings
{
    static readonly string SettingsNamespace = "ThroughputCollector";

    public ThroughputSettings(Broker broker, string transportConnectionString, string serviceControlAPI, string serviceControlQueue, string errorQueue, string persistenceType, string customerName, string serviceControlVersion, string auditQueue = "audit")
    {
        Broker = broker;
        TransportConnectionString = transportConnectionString;
        ServiceControlAPI = serviceControlAPI;
        ServiceControlQueue = serviceControlQueue;
        ErrorQueue = errorQueue;
        PersistenceType = persistenceType;
        AuditQueue = auditQueue;
        CustomerName = customerName;
        ServiceControlVersion = serviceControlVersion;
    }

    public string ServiceControlAPI { get; }
    public Broker Broker { get; set; }
    public string ErrorQueue { get; }
    public string ServiceControlQueue { get; }
    public string AuditQueue { get; } //NOTE can we get this?
    public string TransportConnectionString { get; }
    public string PersistenceType { get; set; }
    public string CustomerName { get; }
    public string ServiceControlVersion { get; }
    public FrozenDictionary<string, string> LoadBrokerSettingValues(IEnumerable<KeyDescriptionPair> brokerKeys) => brokerKeys.ToFrozenDictionary(key => key.Key, key => GetConfigSetting(key.Key));

    string GetConfigSetting(string name) => SettingsReader.Read<string>(new SettingsRootNamespace(SettingsNamespace), name);
}
