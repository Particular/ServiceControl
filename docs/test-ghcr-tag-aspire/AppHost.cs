#:sdk Aspire.AppHost.Sdk@13.4.0
#:package Particular.Aspire.Hosting.ServicePlatform@1.0.0-alpha.4
#:package Aspire.Hosting.RabbitMQ@13.4.0

using Aspire.Hosting;
using Particular.Aspire.Hosting.ServicePlatform.Transport;

var builder = DistributedApplication.CreateBuilder(args);

var transportUserName = builder.AddParameter("transportUserName", "guest", secret: true);
var transportPassword = builder.AddParameter("transportPassword", "guest", secret: true);

var transport = builder.AddRabbitMQ("transport", transportUserName, transportPassword)
    .WithManagementPlugin(15672)
    .WithUrlForEndpoint("management", url => url.DisplayText = "RabbitMQ Management");

builder
    .AddParticularPlatform("particular")
    .WithTransportRabbitMQ(RabbitMqRouting.QuorumConventionalRouting, transport)
    .AddDefaultComponents();

//using positional param for now since there's only one
if (args.Length > 1)
{
    UsePrereleaseImage(builder, args[1]);
} else {
    Console.WriteLine("No arguments provided, defaulting to published 'latest' images'");
}

var app = builder.Build();

await app.RunAsync();

static void UsePrereleaseImage(IDistributedApplicationBuilder builder, string tag)
{
    Console.WriteLine($"Using prerelease image tag: {tag}");
    foreach (var c in builder.Resources.OfType<ContainerResource>())
    {
        if (!c.TryGetLastAnnotation<ContainerImageAnnotation>(out var image))
        {
            continue;
        }

        if (image.Image.StartsWith("particular/servicecontrol"))
        {
            builder
                .CreateResourceBuilder(c)
                .WithImage($"ghcr.io/{image.Image}", tag);
        }
    }
}