#nullable enable
namespace ServiceControl.Transports.RabbitMQ;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

public class RabbitMQQuery(ILogger<RabbitMQQuery> logger, TimeProvider timeProvider, TransportSettings transportSettings) : IBrokerThroughputQuery
{
    HttpClient? httpClient;
    readonly ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions()) // Add retry using the default options
        .AddTimeout(TimeSpan.FromMinutes(2)) // Add timeout if it keeps failing
        .Build();
    readonly List<string> initialiseErrors = [];

    public void Initialise(FrozenDictionary<string, string> settings)
    {
        try
        {
            string? connectionString = transportSettings.ConnectionString;
            var connectionConfiguration = ConnectionConfiguration.Create(connectionString, string.Empty);

            if (!settings.TryGetValue(RabbitMQSettings.UserName, out string? username) ||
                string.IsNullOrEmpty(username))
            {
                logger.LogInformation("Using username from connectionstring");
                username = connectionConfiguration.UserName;
            }

            if (!settings.TryGetValue(RabbitMQSettings.Password, out string? password) ||
                string.IsNullOrEmpty(password))
            {
                logger.LogInformation("Using password from connectionstring");
                password = connectionConfiguration.UserName;
            }

            var defaultCredential = new NetworkCredential(username, password);

            if (!settings.TryGetValue(RabbitMQSettings.API, out string? apiUrl) ||
                string.IsNullOrEmpty(apiUrl))
            {
                apiUrl =
                    $"{(connectionConfiguration.UseTls ? $"https://{connectionConfiguration.Host}:15671" : $"http://{connectionConfiguration.Host}:15672")}";
            }

            httpClient = new HttpClient(new SocketsHttpHandler
            {
                Credentials = defaultCredential,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2)
            })
            { BaseAddress = new Uri(apiUrl) };
        }
        catch (Exception e)
        {
            initialiseErrors.Add(e.Message);
            logger.LogError($"Failed to initialise {nameof(RabbitMQQuery)}");
        }
    }

    public async IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IBrokerQueue brokerQueue, DateOnly startDate,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var queue = (RabbitMQBrokerQueueDetails)brokerQueue;
        var url = $"/api/queues/{HttpUtility.UrlEncode(queue.VHost)}/{HttpUtility.UrlEncode(queue.QueueName)}";

        var node = await pipeline.ExecuteAsync(async token => await httpClient!.GetFromJsonAsync<JsonNode>(url, token), cancellationToken);
        queue.AckedMessages = GetAck();

        // looping for 24hrs, in 4 increments of 15 minutes
        for (var i = 0; i < 24 * 4; i++)
        {
            await Task.Delay(TimeSpan.FromMinutes(15), timeProvider, cancellationToken);

            node = await pipeline.ExecuteAsync(async token => await httpClient!.GetFromJsonAsync<JsonNode>(url, token), cancellationToken);
            var newReading = GetAck();
            if (newReading is not null)
            {
                if (newReading > queue.AckedMessages)
                {
                    yield return new QueueThroughput
                    {
                        DateUTC = DateOnly.FromDateTime(timeProvider.GetUtcNow().DateTime),
                        TotalThroughput = newReading.Value - queue.AckedMessages.Value
                    };
                }
                queue.AckedMessages = newReading;
            }
        }
        yield break;

        long? GetAck()
        {
            if (node!["message_stats"] is JsonObject stats && stats["ack"] is JsonValue val)
            {
                return val.GetValue<long>();
            }
            return null;
        }
    }

    async Task<(string rabbitVersion, string managementVersion)> GetRabbitDetails(bool skipResiliencePipeline, CancellationToken cancellationToken)
    {
        var overviewUrl = "/api/overview";

        JsonObject obj;

        if (skipResiliencePipeline)
        {
            obj = (await httpClient!.GetFromJsonAsync<JsonObject>(overviewUrl, cancellationToken))!;
        }
        else
        {
            obj = (await pipeline.ExecuteAsync(async token =>
                await httpClient!.GetFromJsonAsync<JsonObject>(overviewUrl, token), cancellationToken))!;
        }

        var statsDisabled = obj["disable_stats"]?.GetValue<bool>() ?? false;

        if (statsDisabled)
        {
            throw new Exception("The RabbitMQ broker is configured with 'management.disable_stats = true' or 'management_agent.disable_metrics_collector = true' and as a result queue statistics cannot be collected using this tool. Consider changing the configuration of the RabbitMQ broker.");
        }

        var rabbitVersion = obj["rabbitmq_version"] ?? obj["product_version"];
        var mgmtVersion = obj["management_version"];

        return (rabbitVersion?.GetValue<string>() ?? "Unknown", mgmtVersion?.GetValue<string>() ?? "Unknown");
    }

    public async IAsyncEnumerable<IBrokerQueue> GetQueueNames([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var page = 1;
        bool morePages;
        var vHosts = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

        (string rabbitVersion, string managementVersion) = await GetRabbitDetails(false, cancellationToken);
        Data["RabbitMQVersion"] = rabbitVersion;
        Data["RabbitMQManagementVersionVersion"] = managementVersion;

        do
        {
            (var queues, morePages) = await GetPage(page, cancellationToken);

            if (queues != null)
            {
                foreach (var rabbitMQQueueDetails in queues)
                {
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
            var bindingsUrl = $"/api/queues/{HttpUtility.UrlEncode(brokerQueue.VHost)}/{HttpUtility.UrlEncode(brokerQueue.QueueName)}/bindings";
            var bindings = await pipeline.ExecuteAsync(async token => await httpClient!.GetFromJsonAsync<JsonArray>(bindingsUrl, token), cancellationToken);
            var conventionalBindingFound = bindings?.Any(binding => binding!["source"]?.GetValue<string>() == brokerQueue.QueueName
                                                                    && binding["vhost"]?.GetValue<string>() == brokerQueue.VHost
                                                                    && binding["destination"]?.GetValue<string>() == brokerQueue.QueueName
                                                                    && binding["destination_type"]?.GetValue<string>() == "queue"
                                                                    && binding["routing_key"]?.GetValue<string>() == string.Empty
                                                                    && binding["properties_key"]?.GetValue<string>() == "~") ?? false;

            if (conventionalBindingFound)
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
            var exchangeUrl = $"/api/exchanges/{HttpUtility.UrlEncode(brokerQueue.VHost)}/{HttpUtility.UrlEncode(brokerQueue.QueueName)}/bindings/destination";
            var bindings = await pipeline.ExecuteAsync(async token => await httpClient!.GetFromJsonAsync<JsonArray>(exchangeUrl, token), cancellationToken);
            var delayBindingFound = bindings?.Any(binding =>
            {
                var source = binding!["source"]?.GetValue<string>();

                return source is "nsb.v2.delay-delivery" or "nsb.delay-delivery"
                    && binding["vhost"]?.GetValue<string>() == brokerQueue.VHost
                    && binding["destination"]?.GetValue<string>() == brokerQueue.QueueName
                    && binding["destination_type"]?.GetValue<string>() == "exchange"
                    && binding["routing_key"]?.GetValue<string>() == $"#.{brokerQueue.QueueName}";
            }) ?? false;

            if (delayBindingFound)
            {
                brokerQueue.EndpointIndicators.Add("DelayBinding");
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Clearly no delay binding here
        }
    }

    async Task<(RabbitMQBrokerQueueDetails[]?, bool morePages)> GetPage(int page, CancellationToken cancellationToken)
    {
        var url = $"/api/queues?page={page}&page_size=500&name=&use_regex=false&pagination=true";

        var container = await pipeline.ExecuteAsync(async token => await httpClient!.GetFromJsonAsync<JsonNode>(url, token), cancellationToken);
        switch (container)
        {
            case JsonObject obj:
                {
                    var pageCount = obj["page_count"]!.GetValue<int>();
                    var pageReturned = obj["page"]!.GetValue<int>();

                    if (obj["items"] is not JsonArray items)
                    {
                        return (null, false);
                    }

                    var queues = items.Select(item => new RabbitMQBrokerQueueDetails(item!)).ToArray();

                    return (queues, pageCount > pageReturned);
                }
            // Older versions of RabbitMQ API did not have paging and returned the array of items directly
            case JsonArray arr:
                {
                    var queues = arr.Select(item => new RabbitMQBrokerQueueDetails(item!)).ToArray();

                    return (queues, false);
                }
            default:
                throw new Exception("Was not able to get list of queues from RabbitMQ broker.");
        }
    }

    public string? ScopeType { get; set; }
    public KeyDescriptionPair[] Settings => [
        new KeyDescriptionPair(RabbitMQSettings.API, RabbitMQSettings.APIDescription),
        new KeyDescriptionPair(RabbitMQSettings.UserName, RabbitMQSettings.UserNameDescription),
        new KeyDescriptionPair(RabbitMQSettings.Password, RabbitMQSettings.PasswordDescription)
    ];

    public async Task<(bool Success, List<string> Errors)> TestConnection(CancellationToken cancellationToken)
    {
        if (initialiseErrors.Count > 0)
        {
            return (false, initialiseErrors);
        }

        try
        {
            await GetRabbitDetails(true, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Test connection failed");
            return (false, [ex.Message]);
        }

        return (true, []);
    }

    public Dictionary<string, string> Data { get; } = [];
    public string MessageTransport => "RabbitMQ";

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

