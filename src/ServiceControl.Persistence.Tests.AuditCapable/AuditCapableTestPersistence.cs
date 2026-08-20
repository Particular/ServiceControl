namespace ServiceControl.Persistence.Tests.AuditCapable
{
    using System;
    using System.Linq;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Operations.BodyStorage;
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

            Decorate<IIngestionUnitOfWorkFactory>(services, (inner, provider) =>
                new AuditCapableIngestionUnitOfWorkFactory(inner, provider.GetRequiredService<InMemoryAuditStore>()));
            Decorate<IMessagesViewDataStore>(services, (inner, provider) =>
                new AuditCapableMessagesViewDataStore(inner, provider.GetRequiredService<InMemoryAuditStore>()));
            Decorate<IBodyStorage>(services, (inner, provider) =>
                new AuditCapableBodyStorage(inner, provider.GetRequiredService<InMemoryAuditStore>()));
        }

        public void AddInstaller(IServiceCollection services) => inner.AddInstaller(services);

        static void Decorate<TService>(IServiceCollection services, Func<TService, IServiceProvider, TService> decorate)
            where TService : class
        {
            var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(TService))
                ?? throw new InvalidOperationException($"The delegated persister registered no {typeof(TService).Name}.");

            services.Remove(descriptor);

            services.Add(new ServiceDescriptor(typeof(TService),
                provider => decorate(ResolveInner<TService>(provider, descriptor), provider),
                descriptor.Lifetime));
        }

        static TService ResolveInner<TService>(IServiceProvider provider, ServiceDescriptor descriptor)
            where TService : class
        {
            if (descriptor.ImplementationInstance is TService instance)
            {
                return instance;
            }

            if (descriptor.ImplementationFactory is not null)
            {
                return (TService)descriptor.ImplementationFactory(provider);
            }

            return (TService)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);
        }
    }
}
