namespace ServiceControl.Transport.Tests;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using Particular.Approvals;
using Transports;
using Transports.ASBS;
using Transports.BrokerThroughput;

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
        query.Initialize(ReadOnlyDictionary<string, string>.Empty);
        (bool success, List<string> errors, string diagnostics) =
            await query.TestConnection(cancellationTokenSource.Token);

        Assert.That(success, Is.False);

        Assert.Multiple(() =>
        {
            Assert.That(errors, Has.Member("ClientId is a required setting"));
            Assert.That(errors, Has.Member("ClientSecret is a required setting"));
            Assert.That(errors, Has.Member("TenantId is a required setting"));
        });

        Approver.Verify(diagnostics);
    }

    [Test]
    public async Task TestConnectionWithValidSettings()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        string serviceBusName = query.ExtractServiceBusName();
        Dictionary<string, string> dictionary = GetSettings();
        query.Initialize(new ReadOnlyDictionary<string, string>(dictionary));
        (bool success, _, string diagnostics) = await query.TestConnection(cancellationTokenSource.Token);

        Assert.IsTrue(success);
        Approver.Verify(diagnostics, s =>
        {
            s = s.Replace(dictionary[AzureQuery.AzureServiceBusSettings.TenantId], "xxxxx");
            s = s.Replace(dictionary[AzureQuery.AzureServiceBusSettings.SubscriptionId], "xxxxx");
            s = s.Replace(dictionary[AzureQuery.AzureServiceBusSettings.ClientId], "xxxxx");
            s = s.Replace(serviceBusName, "xxxxx");
            return s;
        });
    }

    [Test]
    public async Task TestConnectionWithMissingLastSlashInManagementUrl()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        string serviceBusName = query.ExtractServiceBusName();
        Dictionary<string, string> dictionary = GetSettings();
        dictionary[AzureQuery.AzureServiceBusSettings.ManagementUrl] = "https://management.azure.com";
        query.Initialize(new ReadOnlyDictionary<string, string>(dictionary));
        (bool success, _, string diagnostics) = await query.TestConnection(cancellationTokenSource.Token);

        Assert.That(success, Is.True);
        Approver.Verify(diagnostics, s =>
        {
            s = s.Replace(dictionary[AzureQuery.AzureServiceBusSettings.TenantId], "xxxxx");
            s = s.Replace(dictionary[AzureQuery.AzureServiceBusSettings.SubscriptionId], "xxxxx");
            s = s.Replace(dictionary[AzureQuery.AzureServiceBusSettings.ClientId], "xxxxx");
            s = s.Replace(serviceBusName, "xxxxx");
            return s;
        });
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
        query.Initialize(new ReadOnlyDictionary<string, string>(dictionary));
        (bool success, List<string> errors, string diagnostics) =
            await query.TestConnection(cancellationTokenSource.Token);

        Assert.That(success, Is.False);
        Assert.That(errors.Single(), Does.StartWith("Invalid tenant id provided"));
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
        query.Initialize(new ReadOnlyDictionary<string, string>(dictionary));
        (bool success, List<string> errors, string diagnostics) =
            await query.TestConnection(cancellationTokenSource.Token);

        Assert.That(success, Is.False);
        Assert.That(errors.Single(), Does.StartWith("The GUID for subscription is invalid"));
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
        query.Initialize(new ReadOnlyDictionary<string, string>(dictionary));
        (bool success, List<string> errors, string diagnostics) =
            await query.TestConnection(cancellationTokenSource.Token);

        Assert.IsFalse(success);
        Assert.That(errors.Single(), Does.StartWith("ClientSecretCredential authentication failed"));
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
        query.Initialize(new ReadOnlyDictionary<string, string>(dictionary));
        (bool success, List<string> errors, _) = await query.TestConnection(cancellationTokenSource.Token);

        Assert.IsFalse(success);
        Assert.That(errors.Single(), Does.StartWith("ClientSecretCredential authentication failed"));
    }

    [Test]
    public async Task RunScenario()
    {
        // We need to wait a bit of time, because Azure metrics take a while to be available
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        CancellationToken token = cancellationTokenSource.Token;
        const int numMessagesToIngest = 15;

        await CreateTestQueue(transportSettings.EndpointName);
        await SendAndReceiveMessages(transportSettings.EndpointName, numMessagesToIngest);

        Dictionary<string, string> dictionary = GetSettings();

        query.Initialize(new ReadOnlyDictionary<string, string>(dictionary));

        var queueNames = new List<IBrokerQueue>();
        await foreach (IBrokerQueue queueName in query.GetQueueNames(token))
        {
            queueNames.Add(queueName);
        }

        IBrokerQueue queue = queueNames.Find(name => name.QueueName == transportSettings.EndpointName);
        Assert.IsNotNull(queue);

        long total = 0L;

        await Task.Delay(TimeSpan.FromMinutes(4), token);

        DateTime startDate = provider.GetUtcNow().DateTime;
        provider.Advance(TimeSpan.FromDays(1));
        await foreach (QueueThroughput queueThroughput in query.GetThroughputPerDay(queue, DateOnly.FromDateTime(startDate), token))
        {
            total += queueThroughput.TotalThroughput;
        }

        Assert.That(total, Is.EqualTo(numMessagesToIngest));
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
}