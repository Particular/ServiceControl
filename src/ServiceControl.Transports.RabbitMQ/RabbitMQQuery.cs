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
    readonly TimeProvider timeProvider;
    readonly Lazy<ManagementClient> managementClient;

    readonly ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
      .AddRetry(new RetryStrategyOptions()) // Add retry using the default options
      .AddTimeout(TimeSpan.FromMinutes(2)) // Add timeout if it keeps failing
      .Build();

    public RabbitMQQuery(ILogger<RabbitMQQuery> logger, TimeProvider timeProvider, ITransportCustomization transportCustomization) : base(logger, "RabbitMQ")
    {
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

    public override async IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IBrokerQueue brokerQueue, DateOnly startDate, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var rabbitBrokerQueue = (RabbitMQBrokerQueue)brokerQueue;
        var queue = await pipeline.ExecuteAsync(async token => await managementClient.Value.GetQueue(rabbitBrokerQueue.QueueName, token), cancellationToken);
        var newReading = new RabbitMQBrokerQueue(queue);

        _ = rabbitBrokerQueue.CalculateThroughputFrom(newReading);

        // looping for 24hrs, in 4 increments of 15 minutes
        for (var i = 0; i < 24 * 4; i++)
        {
            await Task.Delay(TimeSpan.FromMinutes(15), timeProvider, cancellationToken);

            queue = await pipeline.ExecuteAsync(async token => await managementClient.Value.GetQueue(rabbitBrokerQueue.QueueName, token), cancellationToken);
            newReading = new RabbitMQBrokerQueue(queue);

            var newTotalThroughput = rabbitBrokerQueue.CalculateThroughputFrom(newReading);

            yield return new QueueThroughput
            {
                DateUTC = DateOnly.FromDateTime(timeProvider.GetUtcNow().DateTime),
                TotalThroughput = newTotalThroughput
            };
        }
    }

    public override async IAsyncEnumerable<IBrokerQueue> GetQueueNames([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var page = 1;
        bool morePages;

        await GetBrokerDetails(cancellationToken);

        do
        {
            (var queues, morePages) = await pipeline.ExecuteAsync(async token => await managementClient.Value.GetQueues(page, 500, token), cancellationToken);

            foreach (var queue in queues)
            {
                if (queue.Name.StartsWith("nsb.delay-level-") ||
                    queue.Name.StartsWith("nsb.v2.delay-level-") ||
                    queue.Name.StartsWith("nsb.v2.verify-"))
                {
                    continue;
                }

                var brokerQueue = new RabbitMQBrokerQueue(queue);
                await AddEndpointIndicators(brokerQueue, cancellationToken);
                yield return brokerQueue;
            }

            page++;
        } while (morePages);
    }

    async Task GetBrokerDetails(CancellationToken cancellationToken)
    {
        var overview = await pipeline.ExecuteAsync(async async => await managementClient.Value.GetOverview(cancellationToken), cancellationToken);

        if (overview.DisableStats)
        {
            throw new Exception(disableStatsErrorMessage);
        }

        Data["RabbitMQVersion"] = overview.BrokerVersion ?? "Unknown";
    }

    async Task AddEndpointIndicators(RabbitMQBrokerQueue brokerQueue, CancellationToken cancellationToken)
    {
        try
        {
            var bindings = await pipeline.ExecuteAsync(async token => await managementClient.Value.GetBindingsForQueue(brokerQueue.QueueName, token), cancellationToken);

            // Check if conventional binding is found
            if (bindings.Any(binding => binding.Source == brokerQueue.QueueName
                && binding.Destination == brokerQueue.QueueName
                && binding.DestinationType == "queue"
                && binding.RoutingKey == string.Empty
                && binding.PropertiesKey == "~"))
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
            var bindings = await pipeline.ExecuteAsync(async token => await managementClient.Value.GetBindingsForExchange(brokerQueue.QueueName, token), cancellationToken);

            // Check if delayed binding is found
            if (bindings.Any(binding => binding.Source is "nsb.v2.delay-delivery" or "nsb.delay-delivery"
                    && binding.Destination == brokerQueue.QueueName
                    && binding.DestinationType == "exchange"
                    && binding.RoutingKey == $"#.{brokerQueue.QueueName}"))
            {
                brokerQueue.EndpointIndicators.Add("DelayBinding");
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Clearly no delay binding here
        }
    }

    public override KeyDescriptionPair[] Settings => [];

    protected override async Task<(bool Success, List<string> Errors)> TestConnectionCore(CancellationToken cancellationToken)
    {
        try
        {
            var overview = await managementClient.Value.GetOverview(cancellationToken);

            if (overview.DisableStats)
            {
                return (false, [disableStatsErrorMessage]);
            }

            return (true, []);
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Failed to connect to RabbitMQ management API", ex);
        }
    }

    protected override void InitializeCore(ReadOnlyDictionary<string, string> settings) => Diagnostics.AppendLine("Using settings from connection string");

    const string disableStatsErrorMessage = "The RabbitMQ broker is configured with 'management.disable_stats = true' or 'management_agent.disable_metrics_collector = true' and as a result queue statistics cannot be collected using this tool. Consider changing the configuration of the RabbitMQ broker.";
}

