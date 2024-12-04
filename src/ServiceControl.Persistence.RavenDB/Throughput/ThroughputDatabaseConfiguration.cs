#nullable enable
namespace ServiceControl.Persistence.RavenDB.Throughput;

public class ThroughputDatabaseConfiguration(string name)
{
    public string Name { get; } = name;
}