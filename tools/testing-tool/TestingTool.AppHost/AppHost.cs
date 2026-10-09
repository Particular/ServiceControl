using Particular.Aspire.Hosting.ServicePlatform.Platform;
using TestingTool.AppHost;

var options = CliOptions.Parse(args);
var persistenceType = options.GetValue("persistence", PersistenceType.RavenDb);
var imageTag = options.GetValueOrDefault("tag");
Console.WriteLine($"Using persistence type: {persistenceType}");

var builder = DistributedApplication.CreateBuilder(args);

// --- Observability stack (OTel Collector → Jaeger + Prometheus + Grafana) ---
var observability = builder.AddObservabilityStack();

// --- Particular Platform ---

var platform = builder.AddParticularPlatform("particular");
var transport = platform.AddTransport(options.GetValue("transport", TransportType.RabbitMq));

//need to add this unconditionally because the aspire plugin doens't support sql natively yet.
var raven = platform.AddPersistenceRavenDb("raven");

var primaryErrorInstance = platform
    .AddServiceControlErrorInstance("error", raven)
    .WithEnvironment("SERVICECONTROL_MAXIMUMCONCURRENCYLEVEL", "100")
    .WithEnvironment("SERVICECONTROL_ERRORINGESTIONBATCHSIZE", "25")
    .WithEnvironment("SERVICECONTROL_ERRORINGESTIONMAXPARALLELWRITERS", "4")
    .WithEnvironment("SERVICECONTROL_ERRORINGESTIONBATCHTIMEOUT", "00:00:00.100")
    .WithEnvironment("SERVICECONTROL_ALLOWMESSAGEEDITING", "true")
    .WithEnvironment("SERVICECONTROL_DISABLEEXTERNALINTEGRATIONSPUBLISHING", "true")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", observability.Collector.GetEndpoint("otlp-grpc"))
    .WithPersistenceType(persistenceType)
    .WithRunMode(PlatformRunMode.SetupAndRun);

for (int i = 0; i < options.GetValue("error-ingestion-scale-unit", 0); i++) {
    platform
        .AddServiceControlErrorInstance("error-scale-"  + i, raven)
        .WithArgs("--error-ingestion-only")
        .WithEnvironment("SERVICECONTROL_INSTANCENAME", "Error-scale-" + i)
        .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", observability.Collector.GetEndpoint("otlp-grpc"))
        .WithEnvironment("SERVICECONTROL_DISABLEEXTERNALINTEGRATIONSPUBLISHING", "true")
        
        //scale settings
        .WithEnvironment("SERVICECONTROL_MAXIMUMCONCURRENCYLEVEL", "100")
        .WithEnvironment("SERVICECONTROL_ERRORINGESTIONBATCHSIZE", "25")
        .WithEnvironment("SERVICECONTROL_ERRORINGESTIONMAXPARALLELWRITERS", "4")
        .WithEnvironment("SERVICECONTROL_ERRORINGESTIONBATCHTIMEOUT", "00:00:00.100")
        
        //.WaitFor(primaryErrorInstance)
        .WithPersistenceType(persistenceType)
        .WithRunMode(PlatformRunMode.Run);
}

platform.AddServicePulse("pulse", primaryErrorInstance, platform.AddServiceControlMonitoringInstance("monitoring"));

// EF persistence lives in the audit instance itself, so audit load needs a real audit container.
// The platform persistence argument stays RavenDB because the Aspire package only knows about
// platform persistences; the SQL wiring is layered on with WithPersistenceType.
var auditInstanceCount = options.GetValue("audit-instances", 0);

if (auditInstanceCount > 0 && persistenceType != PersistenceType.RavenDb && string.IsNullOrWhiteSpace(imageTag))
{
    Console.WriteLine(
        $"WARNING: --audit-instances with --persistence:{persistenceType} needs a servicecontrol-audit image that "
        + "contains the EF audit persisters. They are newer than the latest release, so the default 'latest' tag "
        + "will fail to start with \"Could not load persistence customization type\". Pass --tag with a prerelease "
        + "that has them, or build the audit image locally.");
}

IResourceBuilder<ServiceControlAuditInstanceResource>? auditOwner = null;

for (int i = 0; i < auditInstanceCount; i++)
{
    var audit = platform.AddServiceControlAuditInstance("audit" + i, primaryErrorInstance, raven)
        .WithEnvironment("SERVICECONTROL_AUDIT_INSTANCENAME", "Audit-" + i)
        .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", observability.Collector.GetEndpoint("otlp-grpc"))
        // Reports the audit ingestion and failed-import custom checks to the primary, which is the
        // only instance ServicePulse asks. Without it a failed audit import is invisible outside
        // the audit container's own log.
        .WithEnvironment("SERVICECONTROL_AUDIT_SERVICECONTROLQUEUEADDRESS", "Particular.ServiceControl")
        .WithIngestionTuning()
        .WithPersistenceType(persistenceType);

    if (auditOwner is null)
    {
        // One owner migrates the database and provisions partitions. Letting every instance run
        // setup against the same database races the EF migrations. Later instances only run, and
        // their retention sweep keeps partitions provisioned from then on.
        auditOwner = audit.WithRunMode(PlatformRunMode.SetupAndRun);
    }
    else
    {
        audit.WithRunMode(PlatformRunMode.Run).WaitFor(auditOwner);
    }
}

// --- Testing tool ---
builder.AddProject<Projects.TestingTool>("testing-tool")
    .WithParticularPlatform(platform)
    .WithEnvironment("TestingTool__ServiceControlApiUrl", primaryErrorInstance.GetEndpoint("http"))
    .WithEnvironment("TestingTool__AutoStartBackgroundNoise", "true")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", observability.Collector.GetEndpoint("otlp-grpc"))
    // this should wait for the platform but cannot because error scale out leaves nodes unhealthy 
    //.WaitFor(primaryErrorInstance)
    .WaitFor(transport)
    .WaitFor(observability.Collector);

// --- Optional: override ServiceControl image tag for prerelease testing ---
builder.UseServiceControlImageTag(imageTag);

builder.Build().Run();