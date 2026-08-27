using Particular.Aspire.Hosting.ServicePlatform.Platform;
using Particular.Aspire.Hosting.ServicePlatform.Transport;
using TestingTool.AppHost;

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

var errorInstance = platform
    .AddServiceControlErrorInstance("error", raven)
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", observability.Collector.GetEndpoint("otlp-grpc"))
    .WithPersistenceType(PersistenceType.PostgreSql)
    .WithRunMode(PlatformRunMode.SetupAndRun);

platform.AddServicePulse("pulse", errorInstance);

// --- Testing tool ---
builder.AddProject<Projects.TestingTool>("testing-tool")
    .WithParticularPlatform(platform)
    .WithEnvironment("TestingTool__ServiceControlApiUrl", errorInstance.GetEndpoint("http"))
    .WithEnvironment("TestingTool__AutoStartBackgroundNoise", "true")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", observability.Collector.GetEndpoint("otlp-grpc"))
    .WaitFor(errorInstance)
    .WaitFor(observability.Collector);

// --- Optional: override ServiceControl image tag for prerelease testing ---
// Pass a tag as the first argument: `aspire run -- pr-1234`
// Omit it to use the default tag configured by the platform.
builder.UseServiceControlImageTag(args.FirstOrDefault());

builder.Build().Run();