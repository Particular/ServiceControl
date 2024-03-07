namespace Particular.ThroughputCollector.Persistence.RavenDb;

public class DatabaseConfiguration(string name)
{
    public string Name { get; } = name;
}
