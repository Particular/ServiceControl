namespace ServiceControl.Transport.Tests;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;
using Particular.Approvals;
using Transports;
using Transports.ASBS;

[TestFixture]
class AzureQueryTests : TransportTestFixture
{
    FakeTimeProvider provider;
    TransportSettings transportSettings;
    AzureQuery query;

    [SetUp]
    public void Initialise()
    {
        provider = new();
        provider.SetUtcNow(DateTimeOffset.UtcNow);
        transportSettings = new TransportSettings
        {
            ConnectionString = configuration.ConnectionString,
            MaxConcurrency = 1,
            EndpointName = Guid.NewGuid().ToString("N")
        };
        query = new AzureQuery(NullLogger<AzureQuery>.Instance, provider, transportSettings);
    }

    [Test]
    public async Task TestConnectionWithEmptySettings()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        transportSettings.ConnectionString =
            "Endpoint=sb://testmenow.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=xxxxxxxxxxxxx";
        query.Initialise(FrozenDictionary<string, string>.Empty);
        (bool success, List<string> errors, string diagnostics) =
            await query.TestConnection(cancellationTokenSource.Token);

        Assert.IsFalse(success);
        CollectionAssert.Contains(errors, "SubscriptionId is a required setting");
        CollectionAssert.Contains(errors, "ClientId is a required setting");
        CollectionAssert.Contains(errors, "ClientSecret is a required setting");
        CollectionAssert.Contains(errors, "TenantId is a required setting");

        Approver.Verify(diagnostics);
    }

    [Test]
    public async Task TestConnectionWithInvalidTenantIdSetting()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        transportSettings.ConnectionString =
            "Endpoint=sb://testmenow.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=xxxxxxxxxxxxx";

        var dictionary = new Dictionary<string, string>
        {
            { AzureQuery.AzureServiceBusSettings.ClientId, "not valid" },
            { AzureQuery.AzureServiceBusSettings.ClientSecret, "not valid" },
            { AzureQuery.AzureServiceBusSettings.TenantId, "not valid" },
            { AzureQuery.AzureServiceBusSettings.SubscriptionId, "not valid" }
        };
        query.Initialise(dictionary.ToFrozenDictionary());
        (bool success, List<string> errors, string diagnostics) =
            await query.TestConnection(cancellationTokenSource.Token);

        Assert.IsFalse(success);
        StringAssert.StartsWith("Invalid tenant id provided", errors.Single());
        Approver.Verify(diagnostics);
    }

    [Test]
    public async Task TestConnectionWithInvalidSubscriptionIdSettings()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        transportSettings.ConnectionString =
            "Endpoint=sb://testmenow.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=xxxxxxxxxxxxx";

        var dictionary = new Dictionary<string, string>
        {
            { AzureQuery.AzureServiceBusSettings.ClientId, "not valid" },
            { AzureQuery.AzureServiceBusSettings.ClientSecret, "not valid" },
            { AzureQuery.AzureServiceBusSettings.TenantId, Guid.Empty.ToString() },
            { AzureQuery.AzureServiceBusSettings.SubscriptionId, "not valid" }
        };
        query.Initialise(dictionary.ToFrozenDictionary());
        (bool success, List<string> errors, string diagnostics) =
            await query.TestConnection(cancellationTokenSource.Token);

        Assert.IsFalse(success);
        StringAssert.StartsWith("The GUID for subscription is invalid", errors.Single());
        Approver.Verify(diagnostics);
    }

    [Test]
    public async Task TestConnectionWithInvalidClientIdSettings()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        transportSettings.ConnectionString =
            "Endpoint=sb://testmenow.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=xxxxxxxxxxxxx";

        Dictionary<string, string> dictionary = GetSettings();
        dictionary[AzureQuery.AzureServiceBusSettings.ClientId] = "not valid";
        query.Initialise(dictionary.ToFrozenDictionary());
        (bool success, List<string> errors, string diagnostics) =
            await query.TestConnection(cancellationTokenSource.Token);

        Assert.IsFalse(success);
        StringAssert.StartsWith("ClientSecretCredential authentication failed", errors.Single());
        Approver.Verify(diagnostics, s =>
        {
            s = s.Replace(dictionary[AzureQuery.AzureServiceBusSettings.TenantId], "xxxxx");
            s = s.Replace(dictionary[AzureQuery.AzureServiceBusSettings.SubscriptionId], "xxxxx");
            return Regex.Replace(s, "^ClientSecretCredential authentication failed: .*$",
                "ClientSecretCredential authentication failed: AADSTS700016: Application with identifier 'not valid' was not found in the directory",
                RegexOptions.Multiline);
        });
    }

    [Test]
    public async Task TestConnectionWithInvalidClientSecretSettings()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        Dictionary<string, string> dictionary = GetSettings();
        dictionary[AzureQuery.AzureServiceBusSettings.ClientSecret] = "not valid";
        query.Initialise(dictionary.ToFrozenDictionary());
        (bool success, List<string> errors, _) = await query.TestConnection(cancellationTokenSource.Token);

        Assert.IsFalse(success);
        StringAssert.StartsWith("ClientSecretCredential authentication failed", errors.Single());
    }

    [Test]
    public async Task RunScenario()
    {
        // We need to wait a bit of time, because Azure metrics take a while to be available
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        CancellationToken token = cancellationTokenSource.Token;
        const int messagesSent = 15;
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

        Dictionary<string, string> dictionary = GetSettings();

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

    static Dictionary<string, string> GetSettings()
    {
        // Doco on this environment variable - https://github.com/Particular/Platform/blob/main/guidelines/github-actions/secrets.md#azure_aci_credentials
        string aciCredentials = Environment.GetEnvironmentVariable("AZURE_ACI_CREDENTIALS");
        var jsonCredentials = JsonNode.Parse(aciCredentials);
        var dictionary = new Dictionary<string, string>
        {
            { AzureQuery.AzureServiceBusSettings.ClientId, jsonCredentials["clientId"].GetValue<string>() },
            { AzureQuery.AzureServiceBusSettings.ClientSecret, jsonCredentials["clientSecret"].GetValue<string>() },
            { AzureQuery.AzureServiceBusSettings.TenantId, jsonCredentials["tenantId"].GetValue<string>() },
            { AzureQuery.AzureServiceBusSettings.SubscriptionId, jsonCredentials["subscriptionId"].GetValue<string>() },
            {
                AzureQuery.AzureServiceBusSettings.ManagementUrl,
                jsonCredentials["resourceManagerEndpointUrl"].GetValue<string>()
            }
        };
        return dictionary;
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