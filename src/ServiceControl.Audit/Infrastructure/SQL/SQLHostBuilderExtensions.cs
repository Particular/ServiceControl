namespace ServiceControl.Audit.Infrastructure.SQL
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using RavenDB;

    static class SqlHostBuilderExtensions
    {
        public static IHostBuilder UseSqlDb(this IHostBuilder hostBuilder,
            Func<HostBuilderContext, (SqlQueryStore, SqlBodyStore, SqlStore)> sqlStoreBuilder)
        {
            hostBuilder.ConfigureServices((ctx, serviceCollection) =>
            {
                var (queryStore, bodyStore, writeStore) = sqlStoreBuilder(ctx);

                serviceCollection.AddSingleton(queryStore);
                serviceCollection.AddSingleton(bodyStore);
                serviceCollection.AddSingleton(writeStore);
                serviceCollection.AddHostedService<SqlDbHostedService>();
            });

            return hostBuilder;
        }
    }
}