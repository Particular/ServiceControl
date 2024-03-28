namespace ServiceControl.Transport.Tests;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;
using Transports;
using Transports.ASBS;

[TestFixture]
class AzureQueryTests : TransportTestFixture
{
    [Test]
    public async Task RunScenario()
    {
        // We need to wait a bit of time, because Azure metrics take a while to be available
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        CancellationToken token = cancellationTokenSource.Token;
        var provider = new FakeTimeProvider();
        provider.SetUtcNow(DateTimeOffset.UtcNow);
        var transportSettings = new TransportSettings
        {
            ConnectionString = configuration.ConnectionString,
            MaxConcurrency = 1,
            EndpointName = Guid.NewGuid().ToString("N")
        };
        const int messagesSent = 15;
        var query = new AzureQuery(NullLogger<AzureQuery>.Instance, provider);
        await Scenario.Define<MyContext>()
            .WithEndpoint(new Receiver(transportSettings.EndpointName), b =>
            b
                .CustomConfig(ec =>
                {
                    configuration.TransportCustomization.CustomizeEndpoint(ec, transportSettings);
                })
                .When(async bus =>
                {
                    for (int i = 0; i < messagesSent; i++)
                    {
                        await bus.SendLocal(new MyMessage());
                    }
                }))
            .Done(context => context.Counter == messagesSent)
            .Run();

        const string servicebusUrlPrefix = "sb://";
        int servicebusUrlPrefixLength = servicebusUrlPrefix.Length;
        int startIndex = transportSettings.ConnectionString.IndexOf(servicebusUrlPrefix);
        if (startIndex == -1)
        {
            startIndex = 0;
        }
        else
        {
            startIndex += servicebusUrlPrefixLength;
        }

        string serviceBusName = transportSettings.ConnectionString.Substring(startIndex,
            transportSettings.ConnectionString.IndexOf('.', startIndex) - startIndex);
        // Doco on this environment variable - https://github.com/Particular/Platform/blob/main/guidelines/github-actions/secrets.md#azure_aci_credentials
        string aciCredentials = Environment.GetEnvironmentVariable("AZURE_ACI_CREDENTIALS");
        var jsonCredentials = JsonNode.Parse(aciCredentials);
        var dictionary = new Dictionary<string, string>
        {
            { AzureQuery.AzureServiceBusSettings.ServiceBusName, serviceBusName },
            { AzureQuery.AzureServiceBusSettings.ClientId, jsonCredentials["clientId"].GetValue<string>() },
            { AzureQuery.AzureServiceBusSettings.ClientSecret, jsonCredentials["clientSecret"].GetValue<string>() },
            { AzureQuery.AzureServiceBusSettings.TenantId, jsonCredentials["tenantId"].GetValue<string>() },
            { AzureQuery.AzureServiceBusSettings.SubscriptionId, jsonCredentials["subscriptionId"].GetValue<string>() },
            { AzureQuery.AzureServiceBusSettings.ManagementUrl, jsonCredentials["managementEndpointUrl"].GetValue<string>() }
        };

        query.Initialise(dictionary.ToFrozenDictionary());

        var queueNames = new List<IBrokerQueue>();
        await foreach (IBrokerQueue queueName in query.GetQueueNames(token))
        {
            queueNames.Add(queueName);
        }

        IBrokerQueue queue = queueNames.Find(name => name.QueueName == transportSettings.EndpointName);
        Assert.IsNotNull(queue);

        long total = 0L;

        await Task.Delay(TimeSpan.FromMinutes(4));

        DateTime startDate = provider.GetUtcNow().DateTime;
        provider.Advance(TimeSpan.FromDays(1));
        await foreach (QueueThroughput queueThroughput in query.GetThroughputPerDay(queue, DateOnly.FromDateTime(startDate), token))
        {
            total += queueThroughput.TotalThroughput;
        }

        Assert.AreEqual(messagesSent, total);
    }

    class Receiver : EndpointConfigurationBuilder
    {
        public Receiver(string endpointName) => EndpointSetup<BasicEndpointSetup>(c =>
        {
            c.EnableInstallers();
            c.UsePersistence<NonDurablePersistence>();
        }).CustomEndpointName(endpointName);

        public class MyMessageHandler(MyContext testContext) : IHandleMessages<MyMessage>
        {
            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                testContext.Counter++;
                return Task.CompletedTask;
            }
        }
    }

    class MyMessage : ICommand;

    class MyContext : ScenarioContext
    {
        public int Counter { get; set; }
    }
}