namespace ServiceControl.Persistence.UnitOfWork
{
    using System;
    using Microsoft.Extensions.DependencyInjection;

    public static class UnitOfWorkServiceCollectionExtensions
    {
        /// <summary>
        /// Register an ingestion unit of work factory that implements all of the components.
        /// </summary>
        public static void AddUnitOfWorkFactory<T>(this IServiceCollection serviceCollection)
            where T : class, IIngestionUnitOfWorkFactory
            => serviceCollection.AddSingleton<IIngestionUnitOfWorkFactory, T>();

        /// <summary>
        /// Register an ingestion unit of work factory that only implements some of the components.
        /// The rest will be handled by RavenDb.
        /// </summary>
        public static void AddPartialUnitOfWorkFactory<T>(this IServiceCollection serviceCollection)
            where T : class, IIngestionUnitOfWorkFactory
        {
            // HINT: Falls back to the Raven implementation for components not implemented
            var ravenImplementation = Type.GetType("ServiceControl.Persistence.RavenDb.RavenDbIngestionUnitOfWorkFactory, ServiceControl.Persistence.RavenDb", true);
            serviceCollection.AddSingleton(ravenImplementation);
            serviceCollection.AddSingleton<T>();
            //HINT: Custom check state dependency is needed by the RavenDB unit of work 
            serviceCollection.AddSingleton<MinimumRequiredStorageState>();
            serviceCollection.AddSingleton<IIngestionUnitOfWorkFactory>(sp =>
                new FallbackIngestionUnitOfWorkFactory(
                    sp.GetRequiredService<T>(),
                    (IIngestionUnitOfWorkFactory)sp.GetRequiredService(ravenImplementation)
                )
            );
        }
    }
}