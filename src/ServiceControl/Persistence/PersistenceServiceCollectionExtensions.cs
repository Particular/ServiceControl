namespace ServiceControl.Persistence
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using ServiceBus.Management.Infrastructure.Settings;

    static class PersistenceServiceCollectionExtensions
    {
        public static void AddPersistence(this IServiceCollection services, Settings settings,
            bool maintenanceMode = false)
        {
            var persistence = PersistenceFactory.Create(settings, maintenanceMode);
            persistence.AddPersistence(services);

            if (settings.AuditDataLocation == AuditDataLocation.Remote)
            {
                // The audit data is on a dedicated host, so the local sources stand aside and the
                // audit routes answer from the configured remotes alone, with this instance as a
                // non-participant rather than an empty one.
                services.AddSingleton<IAuditCountsDataStore, EmptyAuditCountsDataStore>();
                services.AddSingleton<ISagaHistoryDataStore, EmptySagaHistoryDataStore>();
                return;
            }

            // Only an audit capable persister registers these, so the rest fall back to a local source
            // that holds nothing and the audit routes answer from the configured remotes alone.
            services.TryAddSingleton<IAuditCountsDataStore, EmptyAuditCountsDataStore>();
            services.TryAddSingleton<ISagaHistoryDataStore, EmptySagaHistoryDataStore>();
        }
    }
}
