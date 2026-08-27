using Particular.Aspire.Hosting.ServicePlatform.Platform;
using Particular.Aspire.Hosting.ServicePlatform.Transport;
using TestingTool.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

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

// Find the ServiceControl error instance to wire its REST API URL into the testing tool.
var errorInstance = platform
    .AddServiceControlErrorInstance("error", raven)
    .WithPersistenceType(PersistenceType.PostgreSql)
    .WithRunMode(PlatformRunMode.SetupAndRun);

platform.AddServicePulse("pulse", errorInstance);

builder.AddProject<Projects.TestingTool>("testing-tool")
    .WithParticularPlatform(platform)
    .WithEnvironment("TestingTool__ServiceControlApiUrl", errorInstance.GetEndpoint("http"))
    .WithEnvironment("TestingTool__AutoStartBackgroundNoise", "true")
    .WaitFor(errorInstance);

// --- Optional: override ServiceControl image tag for prerelease testing ---
// Pass a tag as the first argument: `aspire run -- pr-1234`
// Defaults to the 'latest' tag configured by AddDefaultComponents.
var tag = args.FirstOrDefault();
tag = "pr-5765";
if (!string.IsNullOrWhiteSpace(tag))
{
    Console.WriteLine($"Using ServiceControl image tag: {tag}");
    foreach (var c in builder.Resources.OfType<ContainerResource>())
    {
        if (c.TryGetLastAnnotation<ContainerImageAnnotation>(out var image) &&
            image.Image.StartsWith("particular/servicecontrol"))
        {
            builder
                .CreateResourceBuilder(c)
                 .WithImage($"ghcr.io/{image.Image}", tag);
        }
    }
}

builder.Build().Run();