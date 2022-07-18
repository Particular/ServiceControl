namespace ServiceControl.Operations
{
    using Microsoft.Extensions.DependencyInjection;

    static class UnitOfWorkServiceCollectionExtensions
    {
        public static void AddUnitOfWorkFactory<T>(this IServiceCollection serviceCollection)
            where T : class, IIngestionUnitOfWorkFactory
            => serviceCollection.AddSingleton<IIngestionUnitOfWorkFactory, T>();

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