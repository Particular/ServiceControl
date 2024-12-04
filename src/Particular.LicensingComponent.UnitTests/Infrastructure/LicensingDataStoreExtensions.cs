namespace Particular.LicensingComponent.UnitTests.Infrastructure;

using Persistence;

static class LicensingDataStoreExtensions
{
    public static DataStoreBuilder CreateBuilder(this ILicensingDataStore dataStore) => new(dataStore);
}