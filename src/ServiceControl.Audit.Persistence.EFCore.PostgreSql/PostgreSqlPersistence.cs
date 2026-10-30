namespace ServiceControl.Audit.Persistence.EFCore.PostgreSql;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using NServiceBus;
using ServiceControl.Audit.Persistence.EFCore.Abstractions;
using ServiceControl.Audit.Persistence.EFCore.DbContexts;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;
using ServiceControl.PostgreSql.TokenAuthentication;

class PostgreSqlPersistence(PostgreSqlPersisterSettings settings) : EFPersistenceBase(settings)
{
    readonly PostgreSqlTokenAuthentication? tokenAuthentication = settings.TokenAuthentication;

    protected override void AddDbContext(IServiceCollection services, EFPersisterSettings settings)
    {
        if (tokenAuthentication is { } authentication)
        {
            services.TryAddSingleton(_ => authentication.CreateTokenProvider());
            services.TryAddSingleton(serviceProvider => PostgreSqlTokenDataSource.Build(settings.ConnectionString, serviceProvider.GetRequiredService<IPostgreSqlTokenProvider>(), TokenLogger(serviceProvider)));
            services.TryAddKeyedSingleton(PostgreSqlRetentionLock.TokenDataSourceKey, (serviceProvider, _) =>
                PostgreSqlTokenDataSource.Build(PostgreSqlRetentionLock.Unpooled(settings.ConnectionString), serviceProvider.GetRequiredService<IPostgreSqlTokenProvider>(), TokenLogger(serviceProvider)));
        }

        services.AddDbContext<PostgreSqlAuditDbContext>((serviceProvider, options) =>
        {
            if (tokenAuthentication is null)
            {
                options.UseNpgsql(settings.ConnectionString, ConfigureNpgsql);
            }
            else
            {
                options.UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>(), ConfigureNpgsql);
            }

            options.ReplaceService<IMigrationsSqlGenerator, AuditNpgsqlMigrationsSqlGenerator>();

            if (settings.Schema is not null)
            {
                ((IDbContextOptionsBuilderInfrastructure)options).AddOrUpdateExtension(new SchemaOptionsExtension(settings.Schema));
                options.ReplaceService<IModelCacheKeyFactory, SchemaModelCacheKeyFactory>();

                // HasDefaultSchema makes the model differ from the migrations snapshot on purpose.
                options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
            }

            if (settings.EnableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging();
            }
        }, ServiceLifetime.Scoped);

        services.AddScoped<AuditDbContext>(provider => provider.GetRequiredService<PostgreSqlAuditDbContext>());

        void ConfigureNpgsql(NpgsqlDbContextOptionsBuilder npgsqlOptions)
        {
            npgsqlOptions.CommandTimeout(settings.CommandTimeout);
            npgsqlOptions.MigrationsHistoryTable(AuditDbContext.MigrationsHistoryTableName, settings.Schema);

            if (settings.EnableRetryOnFailure)
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: settings.MaxRetryCount,
                    maxRetryDelay: TimeSpan.FromSeconds(settings.MaxRetryDelayInSeconds),
                    errorCodesToAdd: null);
            }
        }
    }

    static ILogger TokenLogger(IServiceProvider serviceProvider) => serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(PostgreSqlTokenDataSource));

    protected override void AddPartitionManager(IServiceCollection services) =>
        services.AddSingleton<IAuditPartitionManager, PostgreSqlAuditPartitionManager>();

    protected override void AddQueryServices(IServiceCollection services)
    {
        services.AddSingleton<IFullTextSearchDialect, PostgreSqlFullTextSearchDialect>();
        services.AddSingleton<IRetentionLock, PostgreSqlRetentionLock>();
    }

    protected override void AddEnvironmentProbes(IServiceCollection services)
    {
        services.AddSingleton<IDatabaseHostingProbe, PostgreSqlDatabaseHostingProbe>();
        services.AddSingleton<IStorageFootprintProbe, PostgreSqlStorageFootprintProbe>();
        services.AddSingleton<IStorageIdentityProvider, PostgreSqlStorageIdentityProvider>();
    }

    protected override void AddCustomChecks(EndpointConfiguration endpointConfiguration) =>
        endpointConfiguration.AddCustomCheck<AuditPartitionCustomCheck>();
}
