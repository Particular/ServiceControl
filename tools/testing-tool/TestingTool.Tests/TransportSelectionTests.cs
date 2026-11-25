using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace TestingTool.Tests;

public class TransportSelectionTests
{
    const string ServiceBusConnectionString =
        "Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=ZmFrZQ==";

    [TestCase(ServiceBusConnectionString, "AzureServiceBus")]
    [TestCase("endpoint=SB://example.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=ZmFrZQ==", "AzureServiceBus")]
    [TestCase("amqp://guest:guest@localhost:5672", "RabbitMQ")]
    [TestCase("Server=localhost;Database=Transport;User Id=sa;Password=x;TrustServerCertificate=true", "SqlServer")]
    [TestCase("", "Learning")]
    public void Selects_transport_from_connection_string(string connectionString, string expected) =>
        Assert.That(Select(connectionString).Name, Is.EqualTo(expected));

    [TestCase("Endpoint=https://example.servicebus.windows.net/;SharedAccessKey=ZmFrZQ==")]
    [TestCase("not a connection string")]
    public void Rejects_unrecognised_connection_string(string connectionString) =>
        Assert.Throws<InvalidOperationException>(() => Select(connectionString));

    static SelectedTransport Select(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:transport"] = connectionString })
            .Build();
        var services = new ServiceCollection();
        services.AddTestingToolEndpoint(new TestingToolOptions(), configuration);
        return services.BuildServiceProvider().GetRequiredService<SelectedTransport>();
    }
}
