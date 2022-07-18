namespace ServiceControl.Operations
{
    using Microsoft.Extensions.DependencyInjection;

    static class UnitOfWorkServiceCollectionExtensions
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
            serviceCollection.AddSingleton<RavenDbIngestionUnitOfWorkFactory>();
            serviceCollection.AddSingleton<T>();
            serviceCollection.AddSingleton<IIngestionUnitOfWorkFactory>(sp =>
                new FallbackIngestionUnitOfWorkFactory(
                    sp.GetService<T>(),
                    sp.GetService<RavenDbIngestionUnitOfWorkFactory>()
                )
            );
        }
    }
}