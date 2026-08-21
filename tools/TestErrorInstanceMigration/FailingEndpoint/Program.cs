using NServiceBus;
using NServiceBus.Heartbeat;

const string AuditQueue = "audit";

// WithParticularPlatform injects the ASB connection string as ConnectionStrings__transport.
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__transport")
    ?? throw new InvalidOperationException("Azure Service Bus connection string was not supplied by the AppHost.");

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var endpointConfiguration = new EndpointConfiguration("MigrationTest.FailingEndpoint");
var transport = new AzureServiceBusTransport(connectionString, TopicTopology.Default);
endpointConfiguration.UseTransport(transport);
endpointConfiguration.SendFailedMessagesTo(ParticularPlatformConfig.ErrorQueue);
endpointConfiguration.AuditProcessedMessagesTo(AuditQueue);
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

app.MapPost("/errors", async (IMessageSession messageSession, int? count) =>
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
    usage = "POST /errors?count=1",
    note = "Each generated message always fails, including when retried from ServicePulse."
}));

// Generate one deterministic failure on startup so the error queue is non-empty immediately.
app.Lifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(async () =>
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var messageSession = scope.ServiceProvider.GetRequiredService<IMessageSession>();
            await messageSession.SendLocal(new FailDeterministically
            {
                ErrorId = Guid.NewGuid(),
                RandomPayload = Convert.ToHexString(Guid.NewGuid().ToByteArray())
            });
            Console.WriteLine("Startup failure message sent.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send startup failure message: {ex}");
        }
    });
});

await app.RunAsync();

public sealed class FailDeterministically : IMessage
{
    public Guid ErrorId { get; set; }
    public string RandomPayload { get; set; } = string.Empty;
}

public sealed class FailDeterministicallyHandler : IHandleMessages<FailDeterministically>
{
    public Task Handle(FailDeterministically message, IMessageHandlerContext context) =>
        throw new SimulatedDeterministicFailure($"Error {message.ErrorId} is expected to fail on every attempt.");
}

public sealed class SimulatedDeterministicFailure(string message) : Exception(message);