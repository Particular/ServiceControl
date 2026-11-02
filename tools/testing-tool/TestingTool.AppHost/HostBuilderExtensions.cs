using Particular.Aspire.Hosting.ServicePlatform.Platform;
using Particular.Aspire.Hosting.ServicePlatform.Transport;

namespace TestingTool.AppHost;

public static class HostBuilderExtensions
{
    public static IResourceBuilder<IResourceWithConnectionString> AddSqlServerPersistence(this IDistributedApplicationBuilder builder, string databaseName)
    {
        var resourceName = "sqlserver";
        var dbResourceName = "servicecontrol-sql";
        if (builder.Resources.TryGetByName(dbResourceName, out var existing))
        {
            return builder.CreateResourceBuilder((IResourceWithConnectionString)existing);
        }
        
        var password = builder.AddParameter("sql-password", "Password1!", secret: true);
        var server = builder
            .AddSqlServer(resourceName, password)
            .WithHostPort(1433)
            //custom image with FTS support
            .WithImage("particular/servicecontrol-testing-sqlserver")
            .WithImageRegistry(null)
            .WithDataVolume("test-tool-sql-data");
        return server.AddDatabase(dbResourceName, databaseName);
    }

    public static IResourceBuilder<IResourceWithConnectionString> AddPostgresPersistence(
        this IDistributedApplicationBuilder builder, string databaseName)
    {
        var databaseResource = "servicecontrol-postgres";
        if (builder.Resources.TryGetByName(databaseResource, out var existing))
        {
            return builder.CreateResourceBuilder((IResourceWithConnectionString)existing);
        }

        var postgresPassword = builder.AddParameter("postgres-password", "Password1!", secret: true);
        var postgres = builder
            .AddPostgres("postgres", password: postgresPassword)
            .WithHostPort(5432)
            .WithPgAdmin()
            .WithDataVolume("test-tool-postgres-data");
        return postgres.AddDatabase(databaseResource, databaseName);
    }

    public static IResourceBuilder<ServiceControlErrorInstanceResource> WithPersistenceType(
        this IResourceBuilder<ServiceControlErrorInstanceResource> error, PersistenceType type, string? azureBlobConnectionString = null, S3BodyStorageOptions? s3 = null)
    {
        if (type == PersistenceType.RavenDb)
        {
            //ravenDB is currently set up explicitly even if you aren't using it
            return error;
        }

        var db = type switch
        {
            PersistenceType.SqlServer => error.ApplicationBuilder.AddSqlServerPersistence("ServiceControl"),
            PersistenceType.PostgreSql => error.ApplicationBuilder.AddPostgresPersistence("servicecontrol"),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        // The primary and its scale-out units share one database and body storage location.
        error = azureBlobConnectionString is not null
            ? error
                .WithEnvironment("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "AzureBlob")
                .WithEnvironment("SERVICECONTROL_MESSAGEBODY_AZURE_CONNECTIONSTRING", azureBlobConnectionString)
            : s3 is not null
                ? error
                    .WithEnvironment("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "S3")
                    .WithS3Environment("SERVICECONTROL_MESSAGEBODY_S3_", s3)
                : error
                    .WithEnvironment("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "FileSystem")
                    .WithEnvironment("SERVICECONTROL_MESSAGEBODY_FILESYSTEM_STORAGEPATH", "/tmp/ServiceControlBodyStorage");

        return error
            .WaitFor(db)
            .WithEnvironment("SERVICECONTROL_PERSISTENCETYPE", PersistenceTypeName(type))
            .WithEnvironment("SERVICECONTROL_DATABASE_CONNECTIONSTRING", db);
    }   
    
    

    /// <summary>
    /// Points an Audit instance at the primary's database container with the configured body storage.
    /// </summary>
    /// <remarks>
    /// The setting root namespace is "ServiceControl.Audit", and the settings reader rewrites both
    /// the dot and the slash to an underscore, so ServiceControl.Audit/Database/ConnectionString is
    /// SERVICECONTROL_AUDIT_DATABASE_CONNECTIONSTRING. The unprefixed names the audit container's
    /// Dockerfile bakes in (PersistenceType, AuditRetentionPeriod) are later in the lookup order,
    /// so the namespaced form wins.
    /// </remarks>
    public static IResourceBuilder<ServiceControlAuditInstanceResource> WithPersistenceType(
        this IResourceBuilder<ServiceControlAuditInstanceResource> audit, PersistenceType type, string? azureBlobConnectionString = null, S3BodyStorageOptions? s3 = null)
    {
        if (type == PersistenceType.RavenDb)
        {
            return audit;
        }

        var db = type switch
        {
            PersistenceType.SqlServer => audit.ApplicationBuilder.AddSqlServerPersistence("ServiceControl"),
            PersistenceType.PostgreSql => audit.ApplicationBuilder.AddPostgresPersistence("servicecontrol"),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        // Audit instances need separate body storage locations; see docs/message-body-storage.md.
        audit = azureBlobConnectionString is not null
            ? audit
                .WithEnvironment("SERVICECONTROL_AUDIT_MESSAGEBODY_STORAGETYPE", "AzureBlob")
                .WithEnvironment("SERVICECONTROL_AUDIT_MESSAGEBODY_AZURE_CONNECTIONSTRING", azureBlobConnectionString)
                .WithEnvironment("SERVICECONTROL_AUDIT_MESSAGEBODY_AZURE_CONTAINERNAME", "audit-bodies-" + audit.Resource.Name)
            : s3 is not null
                ? audit
                    .WithEnvironment("SERVICECONTROL_AUDIT_MESSAGEBODY_STORAGETYPE", "S3")
                    .WithS3Environment("SERVICECONTROL_AUDIT_MESSAGEBODY_S3_", s3)
                    .WithEnvironment("SERVICECONTROL_AUDIT_MESSAGEBODY_S3_KEYPREFIX", "audit-bodies-" + audit.Resource.Name + "/")
                : audit
                    .WithEnvironment("SERVICECONTROL_AUDIT_MESSAGEBODY_STORAGETYPE", "FileSystem")
                    .WithEnvironment("SERVICECONTROL_AUDIT_MESSAGEBODY_FILESYSTEM_STORAGEPATH", "/tmp/ServiceControlAuditBodyStorage");

        return audit
            .WaitFor(db)
            .WithEnvironment("SERVICECONTROL_AUDIT_PERSISTENCETYPE", PersistenceTypeName(type))
            .WithEnvironment("SERVICECONTROL_AUDIT_DATABASE_CONNECTIONSTRING", db);
    }

    /// <summary>
    /// The audit equivalent of the ingestion tuning the primary carries. The setting names are
    /// AuditIngestion*, and they are read under the audit namespace, so the primary's
    /// SERVICECONTROL_ERRORINGESTION* values have no effect here.
    /// </summary>
    public static IResourceBuilder<ServiceControlAuditInstanceResource> WithIngestionTuning(
        this IResourceBuilder<ServiceControlAuditInstanceResource> audit) =>
        audit
            .WithEnvironment("SERVICECONTROL_AUDIT_MAXIMUMCONCURRENCYLEVEL", "100")
            .WithEnvironment("SERVICECONTROL_AUDIT_AUDITINGESTIONBATCHSIZE", "25")
            .WithEnvironment("SERVICECONTROL_AUDIT_AUDITINGESTIONMAXPARALLELWRITERS", "4")
            .WithEnvironment("SERVICECONTROL_AUDIT_AUDITINGESTIONBATCHTIMEOUT", "00:00:00.100");

    public static IResourceBuilder<IResourceWithConnectionString> AddTransport(
        this IResourceBuilder<ParticularPlatformResource> platform, TransportType type)
    {
        var builder = platform.ApplicationBuilder;
        switch (type)
        {
            case TransportType.RabbitMq:
                var transportUserName = builder.AddParameter("transportUserName", "guest", secret: true);
                var transportPassword = builder.AddParameter("transportPassword", "guest", secret: true);
                var rabbit = builder.AddRabbitMQ("transport", transportUserName, transportPassword)
                    .WithManagementPlugin(15672)
                    .WithUrlForEndpoint("management", url => url.DisplayText = "RabbitMQ Management");
                platform.WithTransportRabbitMQ(RabbitMqRouting.QuorumConventionalRouting, rabbit);
                return rabbit;
            case TransportType.SqlServer:
                //re-use the persistence DB so it doesn't start two containers when using both with sql
                builder.AddSqlServerPersistence("ServiceControl");
                var server = builder.Resources.Single(x => x.Name == "sqlserver");
                var database =  builder.CreateResourceBuilder((SqlServerServerResource)server).AddDatabase("transport", "Transport");
                platform.WithTransportSqlServer(database);
                return database;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }   

    
    static IResourceBuilder<T> WithS3Environment<T>(this IResourceBuilder<T> builder, string prefix, S3BodyStorageOptions s3)
        where T : IResourceWithEnvironment
    {
        builder = builder.WithEnvironment(prefix + "BUCKETNAME", s3.Bucket);
        if (s3.Region is not null)
        {
            builder = builder.WithEnvironment(prefix + "REGION", s3.Region);
        }

        if (s3.ServiceUrl is not null)
        {
            builder = builder.WithEnvironment(prefix + "SERVICEURL", s3.ServiceUrl);
        }

        if (s3.AccessKeyId is not null && s3.SecretAccessKey is not null)
        {
            builder = builder
                .WithEnvironment(prefix + "ACCESSKEYID", s3.AccessKeyId)
                .WithEnvironment(prefix + "SECRETACCESSKEY", s3.SecretAccessKey);
        }

        return builder;
    }

    static string PersistenceTypeName(PersistenceType persistence) => persistence switch
    {
        PersistenceType.SqlServer => "SQLServer",
        PersistenceType.PostgreSql => "PostgreSQL",
        _ => throw new ArgumentOutOfRangeException(nameof(persistence))
    };
}