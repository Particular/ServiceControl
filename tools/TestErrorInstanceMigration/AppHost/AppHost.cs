using AppHost;
using Particular.Aspire.Hosting.ServicePlatform.Platform;

var mode = MigrationMode.PostMigrationMode;
var enableIngestion = true;
var targetPersistence = TargetPersistence.SqlServer;
var imageTag = "latest";

Console.WriteLine($"Migration mode: {mode}; target persistence: {targetPersistence}; ServiceControl image tag: {imageTag}");

var builder = DistributedApplication.CreateBuilder(args);

// Azure Service Bus transport — the connection string is supplied as a secret parameter.
var asbConnectionString = builder.AddParameter("asb-connection-string", secret: true);
var transport = builder.AddConnectionString("transport", ReferenceExpression.Create($"{asbConnectionString}"));

var platform = builder
    .AddParticularPlatform("particular")
    .WithTransportAzureServiceBus(transport);

var raven = platform.AddPersistenceRavenDb("migration-ravendb")
    .WithVolume("migration-raven-config", "/etc/ravendb")
    .WithVolume("migration-raven-data", "/var/lib/ravendb/data");
var targetDatabase = builder.AddTargetDatabase(targetPersistence);

var errorInstance =
    mode switch
    {
        MigrationMode.PreMigration => platform.AddRavenErrorInstance(raven, imageTag, enableIngestion),
        MigrationMode.PostMigrationMode => platform.AddSqlErrorInstance(raven, targetPersistence, targetDatabase, imageTag),
        _ => throw new Exception()
    };

platform.AddServicePulse("servicepulse", errorInstance!);

builder.AddProject<Projects.FailingEndpoint>("failing-endpoint")
    .WithParticularPlatform(platform);

await builder.Build().RunAsync();
return;



