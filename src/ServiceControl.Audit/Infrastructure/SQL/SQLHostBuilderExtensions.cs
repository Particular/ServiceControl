namespace ServiceControl.Audit.Infrastructure.SQL
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using RavenDB;
    using ServiceControl.Monitoring;
    using Settings;

    static class SqlHostBuilderExtensions
    {
        public static IHostBuilder UseSqlDb(this IHostBuilder hostBuilder,
            Func<HostBuilderContext, (SqlQueryStore, SqlWriteStore, string)> sqlStoreBuilder)
        {
            hostBuilder.ConfigureServices((ctx, serviceCollection) =>
            {
                var (queryStore, writeStore, connectionString) = sqlStoreBuilder(ctx);

                serviceCollection.AddSingleton(queryStore);
                serviceCollection.AddSingleton(writeStore);
                serviceCollection.AddHostedService<SqlDbHostedService>();
                serviceCollection.AddHostedService(sp => new SqlMessageRetentionHostedService(connectionString, sp.GetRequiredService<Settings>()));
            });

            return hostBuilder;
        }
    }
}