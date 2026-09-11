using Particular.Aspire.Hosting.ServicePlatform.Platform;
using TestingTool.AppHost;

var options = CliOptions.Parse(args);
var persistenceType = options.GetValue("persistence", PersistenceType.RavenDb);
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
        
        //get things working...
        .WithEnvironment("MAXIMUMCONCURRENCYLEVEL", "2")
        
        //.WaitFor(primaryErrorInstance)
        .WithPersistenceType(persistenceType)
        .WithRunMode(PlatformRunMode.Run);
}

platform.AddServicePulse("pulse", primaryErrorInstance, platform.AddServiceControlMonitoringInstance("monitoring"));

for (int i = 0; i < options.GetValue("audit-instances", 0); i++)
{
    platform.AddServiceControlAuditInstance("audit" + i, primaryErrorInstance, raven)
        .WithEnvironment("INSTANCENAME", "Audit-" + i);
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
builder.UseServiceControlImageTag(options.GetValueOrDefault("tag"));

builder.Build().Run();