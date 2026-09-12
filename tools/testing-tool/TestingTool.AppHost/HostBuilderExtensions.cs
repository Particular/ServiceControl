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
            .WithPgAdmin()
            .WithDataVolume("test-tool-postgres-data");
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

    
    static string PersistenceTypeName(PersistenceType persistence) => persistence switch
    {
        PersistenceType.SqlServer => "SQLServer",
        PersistenceType.PostgreSql => "PostgreSQL",
        _ => throw new ArgumentOutOfRangeException(nameof(persistence))
    };
}