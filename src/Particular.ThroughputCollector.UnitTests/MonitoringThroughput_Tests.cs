namespace ServiceControl.Transport.Tests;

using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Particular.ThroughputCollector.Contracts;
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
        };

        return base.Setup();
    }

    //[Test]
    //public async Task Should_record_new_endpoint_throughput()
    //{
    //    return Task.FromResult(Assert.IsTrue(true));
    //}
}