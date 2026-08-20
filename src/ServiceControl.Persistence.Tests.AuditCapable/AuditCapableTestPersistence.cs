namespace ServiceControl.Persistence.Tests.AuditCapable
{
    using System;
    using System.Linq;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Persistence.UnitOfWork;

    class AuditCapableTestPersistence(IPersistence inner) : IPersistence
    {
        public void AddPersistence(IServiceCollection services)
        {
            inner.AddPersistence(services);

            services.AddSingleton<InMemoryAuditStore>();
            services.AddSingleton<IFailedAuditImportDataStore, InMemoryFailedAuditImportDataStore>();
            services.AddSingleton<IAuditCountsDataStore, InMemoryAuditCountsDataStore>();
            services.AddSingleton<ISagaHistoryDataStore, InMemorySagaHistoryDataStore>();

            DecorateUnitOfWorkFactory(services);
        }

        public void AddInstaller(IServiceCollection services) => inner.AddInstaller(services);

        static void DecorateUnitOfWorkFactory(IServiceCollection services)
        {
            var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(IIngestionUnitOfWorkFactory))
                ?? throw new InvalidOperationException("The delegated persister registered no ingestion unit of work factory.");

            services.Remove(descriptor);

            services.AddSingleton<IIngestionUnitOfWorkFactory>(provider => new AuditCapableIngestionUnitOfWorkFactory(
                ResolveInnerFactory(provider, descriptor),
                provider.GetRequiredService<InMemoryAuditStore>()));
        }

        static IIngestionUnitOfWorkFactory ResolveInnerFactory(IServiceProvider provider, ServiceDescriptor descriptor)
        {
            if (descriptor.ImplementationInstance is IIngestionUnitOfWorkFactory instance)
            {
                return instance;
            }

            if (descriptor.ImplementationFactory is not null)
            {
                return (IIngestionUnitOfWorkFactory)descriptor.ImplementationFactory(provider);
            }

            return (IIngestionUnitOfWorkFactory)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);
        }
    }
}
