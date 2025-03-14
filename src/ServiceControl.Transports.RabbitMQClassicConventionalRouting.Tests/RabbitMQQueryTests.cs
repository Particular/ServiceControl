namespace ServiceControl.Transport.Tests;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NServiceBus;
using NUnit.Framework;
using ServiceControl.Transports.BrokerThroughput;
using Transports;
using Transports.RabbitMQ;

[TestFixture]
class RabbitMQQueryTests : TransportTestFixture
{
    [Test]
    public async Task GetQueueNames_FindsQueues()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token = cancellationTokenSource.Token;

        var provider = new FakeTimeProvider();

        var transportSettings = new TransportSettings
        {
            ConnectionString = configuration.ConnectionString,
            EndpointName = Guid.NewGuid().ToString("N")
        };

        configuration.TransportCustomization.CustomizePrimaryEndpoint(new EndpointConfiguration(transportSettings.EndpointName), transportSettings);

        var additionalQueues = Enumerable.Range(1, 10).Select(i => $"myqueue{i}").ToArray();
        await configuration.TransportCustomization.ProvisionQueues(transportSettings, additionalQueues);

        var query = new RabbitMQQuery(NullLogger<RabbitMQQuery>.Instance, provider, configuration.TransportCustomization);
        query.Initialize(ReadOnlyDictionary<string, string>.Empty);

        var queueNames = new List<IBrokerQueue>();

        await foreach (IBrokerQueue queueName in query.GetQueueNames(token))
        {
            queueNames.Add(queueName);

            if (queueName.QueueName == transportSettings.EndpointName)
            {
                Assert.That(queueName.EndpointIndicators, Has.Member("ConventionalTopologyBinding"));
            }
        }

        Assert.That(additionalQueues.Concat([transportSettings.EndpointName]), Is.SubsetOf(queueNames.Select(queue => queue.QueueName)));
    }
}