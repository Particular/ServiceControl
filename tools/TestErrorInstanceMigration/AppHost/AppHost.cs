using AppHost;
using Particular.Aspire.Hosting.ServicePlatform.Platform;

var mode = MigrationMode.Step1PreMigration;
var targetPersistence = TargetPersistence.SqlServer;
var enableIngestion = mode != MigrationMode.Step2RetryMessages;
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
        MigrationMode.Step1PreMigration or MigrationMode.Step2RetryMessages => platform.AddRavenErrorInstance(raven, imageTag, enableIngestion),
        MigrationMode.Step3PostMigration => platform.AddSqlErrorInstance(raven, targetPersistence, targetDatabase, imageTag),
        _ => throw new Exception()
    };

platform.AddServicePulse("servicepulse", errorInstance!);

builder.AddProject<Projects.FailingEndpoint>("failing-endpoint")
    .WithEnvironment("CREATE_FAILURES", (mode == MigrationMode.PreMigration).ToString())
    .WithUrlForEndpoint("http", url =>
    {
        url.DisplayText = "Create Errors";
        url.Url += "/createerrors";
    })
    .WithParticularPlatform(platform);

await builder.Build().RunAsync();
return;



