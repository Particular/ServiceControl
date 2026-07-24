namespace ServiceControl.Persistence.UnitOfWork
{
    using Microsoft.Extensions.DependencyInjection;
    using System;

    public static class UnitOfWorkServiceCollectionExtensions
    {
        /// <summary>
        /// Register an ingestion unit of work factory that implements all the components.
        /// </summary>
        public static void AddUnitOfWorkFactory<T>(this IServiceCollection serviceCollection)
            where T : class, IIngestionUnitOfWorkFactory
            => serviceCollection.AddSingleton<IIngestionUnitOfWorkFactory, T>();
    }
}