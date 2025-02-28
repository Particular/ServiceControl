#nullable enable
namespace ServiceControl.Transports.RabbitMQ;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NServiceBus.Transport.RabbitMQ.ManagementApi;
using Polly;
using Polly.Retry;
using ServiceControl.Transports.BrokerThroughput;

public class RabbitMQQuery : BrokerThroughputQuery
{
    readonly ILogger<RabbitMQQuery> logger;
    readonly TimeProvider timeProvider;
    readonly Lazy<ManagementClient> managementClient;

    readonly ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
      .AddRetry(new RetryStrategyOptions()) // Add retry using the default options
      .AddTimeout(TimeSpan.FromMinutes(2)) // Add timeout if it keeps failing
      .Build();

    public RabbitMQQuery(ILogger<RabbitMQQuery> logger, TimeProvider timeProvider, ITransportCustomization transportCustomization) : base(logger, "RabbitMQ")
    {
        this.logger = logger;
        this.timeProvider = timeProvider;

        if (transportCustomization is IManagementClientProvider provider)
        {
            managementClient = provider.GetManagementClient();
        }
        else
        {
            throw new ArgumentException($"Transport customization does not implement {nameof(IManagementClientProvider)}. Type: {transportCustomization.GetType().Name}", nameof(transportCustomization));
        }
    }

    protected override void InitializeCore(ReadOnlyDictionary<string, string> settings)
    {
        //  TODO: Update documentation
        // https://docs.particular.net/servicecontrol/servicecontrol-instances/configuration#usage-reporting-when-using-the-rabbitmq-transport
        CheckLegacySettings(settings, RabbitMQSettings.UserName);
        CheckLegacySettings(settings, RabbitMQSettings.Password);
        CheckLegacySettings(settings, RabbitMQSettings.API);
    }

    void CheckLegacySettings(ReadOnlyDictionary<string, string> settings, string key)
    {
        if (settings.TryGetValue(key, out _))
        {
            logger.LogInformation($"The legacy LicensingComponent/{key} is still defined in the app.config or environment variables");
            _ = Diagnostics.AppendLine($"LicensingComponent/{key} is still defined in the app.config or environment variables");
        }
    }

    public override async IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IBrokerQueue brokerQueue, DateOnly startDate, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var queue = (RabbitMQBrokerQueueDetails)brokerQueue;
        var response = await pipeline.ExecuteAsync(async token => await managementClient.Value.GetQueue(queue.QueueName, token), cancellationToken);

        if (response.Value is null)
        {
            throw new InvalidOperationException($"Could not access RabbitMQ Management API. ({response.StatusCode}: {response.Reason})");
        }

        var newReading = new RabbitMQBrokerQueueDetails(response.Value);

        _ = queue.CalculateThroughputFrom(newReading);

        // looping for 24hrs, in 4 increments of 15 minutes
        for (var i = 0; i < 24 * 4; i++)
        {
            await Task.Delay(TimeSpan.FromMinutes(15), timeProvider, cancellationToken);

            response = await pipeline.ExecuteAsync(async token => await managementClient.Value.GetQueue(queue.QueueName, token), cancellationToken);

            if (response.Value is null)
            {
                throw new InvalidOperationException($"Could not access RabbitMQ Management API. ({response.StatusCode}: {response.Reason})");
            }

            newReading = new RabbitMQBrokerQueueDetails(response.Value);
            var newTotalThroughput = queue.CalculateThroughputFrom(newReading);

            yield return new QueueThroughput
            {
                DateUTC = DateOnly.FromDateTime(timeProvider.GetUtcNow().DateTime),
                TotalThroughput = newTotalThroughput
            };
        }
    }

    async Task GetRabbitDetails(CancellationToken cancellationToken)
    {
        var response = await pipeline.ExecuteAsync(async async => await managementClient.Value.GetOverview(cancellationToken), cancellationToken);

        ValidateResponse(response);

        if (response.Value!.DisableStats)
        {
            throw new Exception("The RabbitMQ broker is configured with 'management.disable_stats = true' or 'management_agent.disable_metrics_collector = true' " +
                "and as a result queue statistics cannot be collected using this tool. Consider changing the configuration of the RabbitMQ broker.");
        }

        Data["RabbitMQVersion"] = response.Value?.BrokerVersion ?? "Unknown";
    }

    void ValidateResponse<T>((HttpStatusCode StatusCode, string Reason, T? Value) response)
    {
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new HttpRequestException($"Request failed with status code {response.StatusCode}: {response.Reason}");
        }

        if (response.Value is null)
        {
            throw new InvalidOperationException("Request was successful, but the response body was null when a value was expected");
        }
    }

    public override async IAsyncEnumerable<IBrokerQueue> GetQueueNames([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var page = 1;
        bool morePages;

        await GetRabbitDetails(cancellationToken);

        do
        {
            (var queues, morePages) = await GetPage(page, cancellationToken);

            if (queues is not null)
            {
                foreach (var rabbitMQQueueDetails in queues)
                {
                    if (rabbitMQQueueDetails.QueueName.StartsWith("nsb.delay-level-") ||
                        rabbitMQQueueDetails.QueueName.StartsWith("nsb.v2.delay-level-") ||
                        rabbitMQQueueDetails.QueueName.StartsWith("nsb.v2.verify-"))
                    {
                        continue;
                    }

                    await AddAdditionalQueueDetails(rabbitMQQueueDetails, cancellationToken);
                    yield return rabbitMQQueueDetails;
                }
            }

            page++;
        } while (morePages);
    }

    async Task AddAdditionalQueueDetails(RabbitMQBrokerQueueDetails brokerQueue, CancellationToken cancellationToken)
    {
        try
        {
            var response = await pipeline.ExecuteAsync(async token => await managementClient.Value.GetBindingsForQueue(brokerQueue.QueueName, token), cancellationToken);

            // Check if conventional binding is found
            if (response.Value.Any(binding => binding?.Source == brokerQueue.QueueName
                && binding?.Destination == brokerQueue.QueueName
                && binding?.DestinationType == "queue"
                && binding?.RoutingKey == string.Empty
                && binding?.PropertiesKey == "~"))
            {
                brokerQueue.EndpointIndicators.Add("ConventionalTopologyBinding");
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Clearly no conventional topology binding here
        }

        try
        {
            var response = await pipeline.ExecuteAsync(async token => await managementClient.Value.GetBindingsForExchange(brokerQueue.QueueName, token), cancellationToken);

            // Check if delayed binding is found
            if (response.Value.Any(binding => binding?.Source is "nsb.v2.delay-delivery" or "nsb.delay-delivery"
                    && binding?.Destination == brokerQueue.QueueName
                    && binding?.DestinationType == "exchange"
                    && binding?.RoutingKey == $"#.{brokerQueue.QueueName}"))
            {
                brokerQueue.EndpointIndicators.Add("DelayBinding");
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Clearly no delay binding here
        }
    }

    internal async Task<(List<RabbitMQBrokerQueueDetails>?, bool morePages)> GetPage(int page, CancellationToken cancellationToken)
    {
        var (StatusCode, Reason, Value, MorePages) = await pipeline.ExecuteAsync(async token => await managementClient.Value.GetQueues(page, 500, token), cancellationToken);

        ValidateResponse((StatusCode, Reason, Value));

        return (MaterializeQueueDetails(Value), MorePages);
    }

    static List<RabbitMQBrokerQueueDetails> MaterializeQueueDetails(List<Queue> items)
    {
        var queues = new List<RabbitMQBrokerQueueDetails>();
        foreach (var item in items)
        {
            queues.Add(new RabbitMQBrokerQueueDetails(item));
        }

        return queues;
    }

    public override KeyDescriptionPair[] Settings =>
    [
        new KeyDescriptionPair(RabbitMQSettings.API, RabbitMQSettings.APIDescription),
        new KeyDescriptionPair(RabbitMQSettings.UserName, RabbitMQSettings.UserNameDescription),
        new KeyDescriptionPair(RabbitMQSettings.Password, RabbitMQSettings.PasswordDescription)
    ];

    protected override async Task<(bool Success, List<string> Errors)> TestConnectionCore(CancellationToken cancellationToken)
    {
        try
        {
            var (statusCode, reason, value) = await managementClient.Value.GetOverview(cancellationToken);

            if (value is not null)
            {
                return (true, []);
            }
            else
            {
                return (false, [$"{statusCode}: {reason}"]);
            }
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Failed to connect to RabbitMQ management API", ex);
        }
    }

    public static class RabbitMQSettings
    {
        public static readonly string API = "RabbitMQ/ApiUrl";
        public static readonly string APIDescription = "RabbitMQ management URL";
        public static readonly string UserName = "RabbitMQ/UserName";
        public static readonly string UserNameDescription = "Username to access the RabbitMQ management interface";
        public static readonly string Password = "RabbitMQ/Password";
        public static readonly string PasswordDescription = "Password to access the RabbitMQ management interface";
    }
}

