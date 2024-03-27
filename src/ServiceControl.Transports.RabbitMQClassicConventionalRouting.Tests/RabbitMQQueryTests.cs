namespace ServiceControl.Transport.Tests;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using Transports;
using Transports.RabbitMQ;

[TestFixture]
class RabbitMQQueryTests : TransportTestFixture
{
    [Test]
    public async Task GetQueueNames_FindsQueues()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        CancellationToken token = cancellationTokenSource.Token;
        var provider = new FakeTimeProvider();
        var transportSettings = new TransportSettings
        {
            ConnectionString = configuration.ConnectionString,
            MaxConcurrency = 1,
            EndpointName = Guid.NewGuid().ToString("N")
        };
        var query = new RabbitMQQuery(provider, transportSettings);
        string[] additionalQueues = Enumerable.Range(1, 10).Select(i => $"myqueue{i}").ToArray();
        await configuration.TransportCustomization.ProvisionQueues(transportSettings, additionalQueues);

        var dictionary = new Dictionary<string, string>();
        query.Initialise(dictionary.ToFrozenDictionary());

        var queueNames = new List<IBrokerQueue>();
        await foreach (IBrokerQueue queueName in query.GetQueueNames(token))
        {
            queueNames.Add(queueName);
            Assert.AreEqual("/", queueName.Scope);
            if (queueName.QueueName == transportSettings.EndpointName)
            {
                CollectionAssert.Contains(queueName.EndpointIndicators, "DelayBinding");
                CollectionAssert.Contains(queueName.EndpointIndicators, "ConventionalTopologyBinding");
            }
        }

        CollectionAssert.IsSubsetOf(additionalQueues.Concat([transportSettings.EndpointName]), queueNames.Select(queue => queue.QueueName));
    }
}