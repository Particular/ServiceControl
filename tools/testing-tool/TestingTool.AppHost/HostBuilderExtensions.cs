using Particular.Aspire.Hosting.ServicePlatform.Platform;

namespace TestingTool.AppHost;

public static class HostBuilderExtensions
{
    public static IResourceBuilder<IResourceWithConnectionString> AddSqlServerPersistence(this IDistributedApplicationBuilder builder, string databaseName)
    {
        var resourceName = "sqlserver";
        if (builder.Resources.TryGetByName(resourceName, out var existing))
        {
            return builder.CreateResourceBuilder((IResourceWithConnectionString)existing);
        }
        
        var password = builder.AddParameter("sql-password", "Password1!", secret: true);
        var server = builder
            .AddSqlServer(resourceName, password)
            .WithDataVolume("migration-sql-data");
        return server.AddDatabase("servicecontrol-sql", databaseName);
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
            .WithPgAdmin()
            .WithDataVolume("migration-postgres-data");
        return postgres.AddDatabase(databaseResource, databaseName);
    }

    public static IResourceBuilder<ServiceControlErrorInstanceResource> WithPersistenceType(
        this IResourceBuilder<ServiceControlErrorInstanceResource> error, PersistenceType type)
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

        return error
            .WaitFor(db)
            //file storage for now
            .WithEnvironment("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "FileSystem")
            .WithEnvironment("SERVICECONTROL_MESSAGEBODY_FILESYSTEM_STORAGEPATH", "/tmp/ServiceControlBodyStorage")
            .WithEnvironment("SERVICECONTROL_PERSISTENCETYPE", PersistenceTypeName(type))
            .WithEnvironment("SERVICECONTROL_DATABASE_CONNECTIONSTRING", db);
    }   
    
    static string PersistenceTypeName(PersistenceType persistence) => persistence switch
    {
        PersistenceType.SqlServer => "SQLServer",
        PersistenceType.PostgreSql => "PostgreSQL",
        _ => throw new ArgumentOutOfRangeException(nameof(persistence))
    };
}