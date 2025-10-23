namespace ServiceControl.Persistence
{
    using Microsoft.Extensions.Hosting;

    static class PersistenceServiceCollectionExtensions
    {
        public static void AddPersistence(
            this IHostApplicationBuilder hostBuilder
        )
        {
            var persistence = PersistenceFactory.Create(hostBuilder.Configuration);
            persistence.AddPersistence(hostBuilder.Services, hostBuilder.Configuration);
        }
    }
}
