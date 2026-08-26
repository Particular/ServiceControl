using Particular.Aspire.Hosting.ServicePlatform.Platform;

var builder = DistributedApplication.CreateBuilder(args);

// --- Particular Platform (ServiceControl + Learning transport + RavenDB persistence) ---
// AddDefaultComponents wires up the Learning transport, RavenDB, ServiceControl error/audit/monitoring
// instances, and ServicePulse — a complete local platform in one call.

var platform = builder
    .AddParticularPlatform("particular")
    .AddDefaultComponents();

// Find the ServiceControl error instance to wire its REST API URL into the testing tool.
var errorInstance = builder.Resources.OfType<ServiceControlErrorInstanceResource>().First();
var errorInstanceBuilder = builder.CreateResourceBuilder(errorInstance);

// --- Jaeger (OTLP collector + UI) ---
// All-in-one Jaeger accepts OTLP (gRPC :4317) directly and serves the Jaeger UI on :16686.

/* Removing jager for now */
var jaeger = builder.AddContainer("jaeger", "jaegertracing/all-in-one", "1.62")
    .WithHttpEndpoint(targetPort: 16686, name: "ui")
    .WithEndpoint(targetPort: 4317, name: "otlp-grpc")
    .WithUrlForEndpoint("ui", url => url.DisplayText = "Jaeger UI");

// --- Testing Tool ---
// Added as a .NET project so it can be debugged locally. WithParticularPlatform wires the
// transport connection string and license. The ServiceControl REST API URL and OTLP endpoint
// are injected as environment variables so the testing tool can drive replay/search jobs and
// export telemetry.

builder.AddProject<Projects.TestingTool>("testing-tool")
    .WithParticularPlatform(platform)
    .WithEnvironment("TestingTool__ServiceControlApiUrl", errorInstanceBuilder.GetEndpoint("http"))
    //.WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT",
    //    ReferenceExpression.Create($"http://{jaeger.GetEndpoint("otlp-grpc")}"))
    .WithEnvironment("TestingTool__AutoStartBackgroundNoise", "true")
    .WithEnvironment("TestingTool__ReplayEnabled", "true")
    .WithEnvironment("TestingTool__SearchEnabled", "true")
    .WaitFor(errorInstanceBuilder);

// --- Optional: override ServiceControl image tag for prerelease testing ---
// Pass a tag as the first argument: `aspire run AppHost.cs -- pr-1234`
// Defaults to the 'latest' tag configured by AddDefaultComponents.

if (args.Length > 0)
{
    var tag = args[0];
    Console.WriteLine($"Using ServiceControl image tag: {tag}");
    foreach (var c in builder.Resources.OfType<ContainerResource>())
    {
        if (c.TryGetLastAnnotation<ContainerImageAnnotation>(out var image) &&
            (image.Image.StartsWith("particular/servicecontrol") ||
             image.Image.StartsWith("particular/servicepulse")))
        {
            builder
                .CreateResourceBuilder(c)
                .WithImageTag(tag);
        }
    }
}

builder.Build().Run();