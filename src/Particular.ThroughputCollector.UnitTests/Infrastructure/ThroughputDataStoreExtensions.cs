namespace Particular.ThroughputCollector.UnitTests.Infrastructure;

using Particular.ThroughputCollector.Persistence;

static class ThroughputDataStoreExtensions
{
    public static DataStoreBuilder CreateBuilder(this IThroughputDataStore dataStore) => new(dataStore);
}
