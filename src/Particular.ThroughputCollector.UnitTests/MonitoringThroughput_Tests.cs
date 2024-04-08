namespace Particular.ThroughputCollector.UnitTests;

using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Transport;
using NUnit.Framework;
using Particular.ThroughputCollector.Contracts;
using Particular.ThroughputCollector.MonitoringThroughput;
using Particular.ThroughputCollector.UnitTests.Infrastructure;
using ServiceControl.Api;

[TestFixture]
class MonitoringThroughput_Tests : ThroughputCollectorTestFixture
{
    readonly Broker broker = Broker.AzureServiceBus;

    public override Task Setup()
    {
        SetThroughputSettings = s => s.Broker = broker;

        SetExtraDependencies = d =>
        {
            d.AddSingleton<IConfigurationApi, FakeConfigurationApi>();
            d.AddSingleton<IEndpointsApi, FakeEndpointApi>();
            d.AddSingleton<IAuditCountApi, FakeAuditCountApi>();
            //d.AddSingleton<ILogger<MonitoringThroughput_Tests>>
        };

        return base.Setup();
    }

    [Test]
    public async Task Should_record_new_endpoint_and_throughput()
    {
        var monitoringFeature = new MonitoringThroughputFeature();

        var message = new RecordEndpointThroughputData()
        {
            StartDateTime = DateTime.UtcNow.AddMinutes(-5),
            EndDateTime = DateTime.UtcNow,
            EndpointThroughputData = new EndpointThroughputData[] { new EndpointThroughputData { Name = "Endpoint1", Throughput = 15 } }
        };

        var messageBytes = JsonSerializer.SerializeToUtf8Bytes(message);
        var context = new MessageContext(Guid.NewGuid().ToString(), [], messageBytes, new TransportTransaction(), "ServiceControl.ThroughputData", new NServiceBus.Extensibility.ContextBag());
        await monitoringFeature.OnMessage(configuration.ServiceProvider, context, default);


        var foundEndpoint = await DataStore.GetEndpoint("Endpoint1", ThroughputSource.Monitoring);

        Assert.That(foundEndpoint, Is.Not.Null, "Expected to find Endpoint1");
        Assert.That(foundEndpoint.Id.Name, Is.EqualTo("Endpoint1"), "Expected name to be Endpoint1");
        Assert.That(foundEndpoint.EndpointIndicators, Is.Not.Null, "Expected to find endpoint indicators");
        Assert.That(foundEndpoint.EndpointIndicators.Contains(EndpointIndicator.KnownEndpoint.ToString()), Is.True, "Expected KnownEndpoint indicator");

        var foundEndpointThroughput = await DataStore.GetEndpointThroughputByQueueName(["Endpoint1"]);
        Assert.That(foundEndpointThroughput, Is.Not.Null, "Expected endpoint throughput");
        Assert.That(foundEndpointThroughput.ContainsKey("Endpoint1"), Is.True, "Expected throughput for Endpoint1");

        var throughputData = foundEndpointThroughput["Endpoint1"].ToArray();
        Assert.That(throughputData.Length, Is.EqualTo(1), "Expected 1 throughput data for Endpoint1");
        Assert.That(throughputData[0].ThroughputSource, Is.EqualTo(ThroughputSource.Monitoring), "Expected ThroughputSource to be Monitoring for Endpoint1");
        Assert.That(throughputData[0].Keys.Contains(DateOnly.FromDateTime(message.EndDateTime.Date)), Is.True, $"Expected throughput for {message.StartDateTime.Date} for Endpoint1");
        Assert.That(throughputData[0][DateOnly.FromDateTime(message.EndDateTime.Date)], Is.EqualTo(15), $"Expected throughput for {message.StartDateTime.Date} to be 15 for Endpoint1");
    }
}