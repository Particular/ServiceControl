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
using System.Web;
using Microsoft.Extensions.Logging;
using NServiceBus;
using Polly;
using Polly.Retry;
using ServiceControl.Transports.BrokerThroughput;
using NServiceBus.Transport.RabbitMQ.ManagementApi;

public class RabbitMQQuery : BrokerThroughputQuery
{
    HttpClient? httpClient;
    readonly ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions()) // Add retry using the default options
        .AddTimeout(TimeSpan.FromMinutes(2)) // Add timeout if it keeps failing
        .Build();
    readonly ILogger<RabbitMQQuery> logger;
    readonly TimeProvider timeProvider;
    readonly RabbitMQTransport rabbitMQTransport;

    public RabbitMQQuery(ILogger<RabbitMQQuery> logger,
        TimeProvider timeProvider,
        TransportSettings transportSettings,
        ITransportCustomization transportCustomization) : base(logger, "RabbitMQ")
    {
        this.logger = logger;
        this.timeProvider = timeProvider;
        rabbitMQTransport = GetRabbitMQTransport(transportCustomization);
    }

    protected override void InitializeCore(ReadOnlyDictionary<string, string> settings)
    {
        //  TODO: Update documentation
        // https://docs.particular.net/servicecontrol/servicecontrol-instances/configuration#usage-reporting-when-using-the-rabbitmq-transport
        CheckLegacySettings(settings, RabbitMQSettings.UserName);
        CheckLegacySettings(settings, RabbitMQSettings.Password);
        CheckLegacySettings(settings, RabbitMQSettings.API);
    }

    static RabbitMQTransport GetRabbitMQTransport(ITransportCustomization transportCustomization)
    {
        if (transportCustomization is IRabbitMQTransportExtensions rabbitMQTransportCustomization)
        {
            return rabbitMQTransportCustomization.GetTransport();
        }

        throw new InvalidOperationException($"Expected a RabbitMQTransport but received {transportCustomization.GetType().Name}.");
    }

    void CheckLegacySettings(ReadOnlyDictionary<string, string> settings, string key)
    {
        if (settings.TryGetValue(key, out _))
        {
            logger.LogInformation($"The legacy LicensingComponent/{key} is still defined in the app.config or environment variables");
            _ = Diagnostics.AppendLine($"LicensingComponent/{key} is still defined in the app.config or environment variables");
        }
    }

    public override async IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IBrokerQueue brokerQueue,
        DateOnly startDate,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var queue = (RabbitMQBrokerQueueDetails)brokerQueue;
        var url = $"/api/queues/{HttpUtility.UrlEncode(queue.VHost)}/{HttpUtility.UrlEncode(queue.QueueName)}";

        logger.LogDebug($"Querying {url}");

        var response = await pipeline.ExecuteAsync(async token => await rabbitMQTransport.ManagementClient.GetQueue(queue.QueueName, cancellationToken), cancellationToken);

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
            logger.LogDebug($"Querying {url}");
            response = await pipeline.ExecuteAsync(async token => await rabbitMQTransport.ManagementClient.GetQueue(queue.QueueName, cancellationToken), cancellationToken);

            if (response.Value is not null)
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

    async Task GetRabbitDetails(bool skipResiliencePipeline, CancellationToken cancellationToken)
    {

        var response = skipResiliencePipeline
            ? await rabbitMQTransport.ManagementClient.GetOverview(cancellationToken)
            : await pipeline.ExecuteAsync(async async => await rabbitMQTransport.ManagementClient.GetOverview(cancellationToken), cancellationToken);

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
        var vHosts = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

        await GetRabbitDetails(false, cancellationToken);

        do
        {
            (var queues, morePages) = await GetPage(page, cancellationToken);

            if (queues != null)
            {
                foreach (var rabbitMQQueueDetails in queues)
                {
                    if (rabbitMQQueueDetails.QueueName.StartsWith("nsb.delay-level-") ||
                        rabbitMQQueueDetails.QueueName.StartsWith("nsb.v2.delay-level-") ||
                        rabbitMQQueueDetails.QueueName.StartsWith("nsb.v2.verify-"))
                    {
                        continue;
                    }
                    vHosts.Add(rabbitMQQueueDetails.VHost);
                    await AddAdditionalQueueDetails(rabbitMQQueueDetails, cancellationToken);
                    yield return rabbitMQQueueDetails;
                }
            }

            page++;
        } while (morePages);

        ScopeType = vHosts.Count > 1 ? "VirtualHost" : null;
    }

    async Task AddAdditionalQueueDetails(RabbitMQBrokerQueueDetails brokerQueue, CancellationToken cancellationToken)
    {
        try
        {
            var response = await pipeline.ExecuteAsync(async token => await rabbitMQTransport.ManagementClient.GetBindingsForQueue(brokerQueue.QueueName, cancellationToken), cancellationToken);

            // Check if conventional binding is found
            if (response.Value.Any(binding => binding?.Source == brokerQueue.QueueName
                && binding?.Vhost == brokerQueue.VHost
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
            var response = await pipeline.ExecuteAsync(async token => await rabbitMQTransport.ManagementClient.GetBindingsForExchange(brokerQueue.QueueName, cancellationToken), cancellationToken);

            // Check if delayed binding is found
            if (response.Value.Any(binding => binding?.Source is "nsb.v2.delay-delivery" or "nsb.delay-delivery"
                    && binding?.Vhost == brokerQueue.VHost
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
        var (StatusCode, Reason, Value, MorePages) = await pipeline.ExecuteAsync(async token => await rabbitMQTransport.ManagementClient.GetQueues(page, 500, cancellationToken), cancellationToken);

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

    protected override async Task<(bool Success, List<string> Errors)> TestConnectionCore(
        CancellationToken cancellationToken)
    {
        try
        {
            await GetRabbitDetails(true, cancellationToken);
        }
        catch (HttpRequestException e)
        {
            throw new Exception($"Failed to connect to '{httpClient!.BaseAddress}'", e);
        }

        return (true, []);
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

