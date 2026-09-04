using Particular.Aspire.Hosting.ServicePlatform.Platform;
using Particular.Aspire.Hosting.ServicePlatform.Transport;
using TestingTool.AppHost;

var options = CliOptions.Parse(args);
var persistenceType = options.GetValue("persistence", PersistenceType.RavenDb);
Console.WriteLine($"Using persistence type: {persistenceType}");

var builder = DistributedApplication.CreateBuilder(args);

// --- Observability stack (OTel Collector → Jaeger + Prometheus + Grafana) ---
var observability = builder.AddObservabilityStack();

// --- Particular Platform (ServiceControl + RabbitMQ transport) ---
var transportUserName = builder.AddParameter("transportUserName", "guest", secret: true);
var transportPassword = builder.AddParameter("transportPassword", "guest", secret: true);
var transport = builder.AddRabbitMQ("transport", transportUserName, transportPassword)
    .WithManagementPlugin(15672)
    .WithUrlForEndpoint("management", url => url.DisplayText = "RabbitMQ Management");

var platform = builder
    .AddParticularPlatform("particular")
    .WithTransportRabbitMQ(RabbitMqRouting.QuorumConventionalRouting, transport);

var raven = platform.AddPersistenceRavenDb("raven");

var primaryErrorInstance = platform
    .AddServiceControlErrorInstance("error", raven)
    .WithEnvironment("SERVICECONTROL_INSTANCENAME", "booger")
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

platform.AddServicePulse("pulse", primaryErrorInstance);

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