// WithParticularPlatform injects the ASB connection string as ConnectionStrings__transport.
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__transport")
    ?? throw new InvalidOperationException("Azure Service Bus connection string was not supplied by the AppHost.");

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var endpointConfiguration = new EndpointConfiguration("MigrationTest.FailingEndpoint");
var transport = new AzureServiceBusTransport(connectionString, TopicTopology.Default);
endpointConfiguration.UseTransport(transport);
endpointConfiguration.SendFailedMessagesTo(ParticularPlatformConfig.ErrorQueue);
endpointConfiguration.EnableInstallers();
endpointConfiguration.UseSerialization<SystemJsonSerializer>();
endpointConfiguration.Recoverability()
    .Immediate(retries => retries.NumberOfRetries(0))
    .Delayed(retries => retries.NumberOfRetries(0));

// Send heartbeats so ServicePulse discovers the endpoint and shows it as active.
endpointConfiguration.SendHeartbeatTo(
    serviceControlQueue: ParticularPlatformConfig.ServiceControlQueue,
    frequency: TimeSpan.FromSeconds(10));

builder.Services.AddNServiceBusEndpoint(endpointConfiguration);

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/createerrors", async (IMessageSession messageSession, int? count) =>
{
    var numberOfErrors = Math.Clamp(count ?? 1, 1, 100);
    var ids = new Guid[numberOfErrors];

    for (var i = 0; i < numberOfErrors; i++)
    {
        ids[i] = Guid.NewGuid();
        await messageSession.SendLocal(new FailDeterministically
        {
            ErrorId = ids[i],
            RandomPayload = Convert.ToHexString(Guid.NewGuid().ToByteArray())
        });
    }

    return Results.Accepted(value: new { count = numberOfErrors, ids });
});

app.MapGet("/", () => Results.Ok(new
{
    usage = "GET /createerrors",
    note = "Each generated message always fails, including when retried from ServicePulse."
}));

await app.RunAsync();