using Microsoft.Extensions.Hosting;
using Particular.Aspire.Hosting.ServicePlatform.Platform;

namespace AppHost;

public static class BuilderExtensions
{
    public static IResourceBuilder<IResourceWithConnectionString> AddTargetDatabase(this IDistributedApplicationBuilder builder, TargetPersistence persistence)
    {
        if (persistence == TargetPersistence.SqlServer)
        {
            var password = builder.AddParameter("sql-password", secret: true);
            var server = builder.AddSqlServer("sqlserver", password)
                .WithDataVolume("migration-sql-data");
            return server.AddDatabase("servicecontrol-sql", "ServiceControl");
        }

        var postgresPassword = builder.AddParameter("postgres-password", secret: true);
        var postgres = builder.AddPostgres("postgres", password: postgresPassword)
            .WithDataVolume("migration-postgres-data");
        return postgres.AddDatabase("servicecontrol-postgres", "servicecontrol");
    }

    public static IResourceBuilder<ServiceControlErrorInstanceResource> AddRavenErrorInstance(
        this IResourceBuilder<ParticularPlatformResource> platform,
        IResourceBuilder<IResource> raven,
        string imageTag,
        bool ingestErrors)
    {

        var instance = platform
            .AddServiceControlErrorInstance("old-error-ravendb", raven)
            .WithErrorQueueName(ParticularPlatformConfig.ErrorQueue);
        
        ApplyImage(instance, "particular/servicecontrol", imageTag);

        if (!ingestErrors)
        {
            // Keep the old API available for retry/archive operations while leaving newly failed
            // and deterministically re-failed messages in the queue for the replacement instance.
            instance.WithEnvironment("SERVICECONTROL_INGESTERRORMESSAGES", "false");
        }

        return instance;
    }
    
    public static IResourceBuilder<ServiceControlErrorInstanceResource> AddSqlErrorInstance(
        this IResourceBuilder<ParticularPlatformResource> platform,
        IResourceBuilder<IResource> raven,
        TargetPersistence targetPersistence,
        IResourceBuilder<IResourceWithConnectionString> sqlDb,
        string imageTag)
    {
        var instance = platform
            .AddServiceControlErrorInstance("error-sql", raven)
            .WithEnvironment("SERVICECONTROL_PERSISTENCETYPE", PersistenceTypeName(targetPersistence))
            .WithEnvironment("SERVICECONTROL_DATABASE_CONNECTIONSTRING", sqlDb.Resource.ConnectionStringExpression)
            .WithEnvironment("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "FileSystem")
            .WithEnvironment("SERVICECONTROL_MESSAGEBODY_FILESYSTEM_STORAGEPATH", "/var/lib/servicecontrol/message-bodies")
            .WithVolume("migration-target-message-bodies", "/var/lib/servicecontrol/message-bodies")
            .WithErrorQueueName(ParticularPlatformConfig.ErrorQueue);
        
        ApplyImage(instance, "particular/servicecontrol", imageTag);

        return instance;
    }
    
    static string PersistenceTypeName(TargetPersistence persistence) => persistence switch
    {
        TargetPersistence.SqlServer => "SQLServer",
        TargetPersistence.PostgreSql => "PostgreSQL",
        _ => throw new ArgumentOutOfRangeException(nameof(persistence))
    };
    
    static void ApplyImage<T>(this IResourceBuilder<T> resource, string image, string tag) where T : ContainerResource
    {
        // Non-latest tags are CI images, matching the existing GHCR test harness convention.
        if (!string.Equals(tag, "latest", StringComparison.OrdinalIgnoreCase))
        {
            resource.WithImage($"ghcr.io/{image}", tag);
        }
    }
}