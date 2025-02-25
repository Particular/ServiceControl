#nullable enable
namespace ServiceControl.Transports.RabbitMQ;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using ServiceControl.Transports.BrokerThroughput;

public class RabbitMQQuery : BrokerThroughputQuery
{
    HttpClient? httpClient;
    readonly ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions()) // Add retry using the default options
        .AddTimeout(TimeSpan.FromMinutes(2)) // Add timeout if it keeps failing
        .Build();
    readonly ILogger<RabbitMQQuery> logger;
    readonly TimeProvider timeProvider;
    readonly ConnectionConfiguration connectionConfiguration;

    public RabbitMQQuery(ILogger<RabbitMQQuery> logger,
        TimeProvider timeProvider,
        TransportSettings transportSettings) : base(logger, "RabbitMQ")
    {
        this.logger = logger;
        this.timeProvider = timeProvider;

        connectionConfiguration = ConnectionConfiguration.Create(transportSettings.ConnectionString, string.Empty);
    }

    protected override void InitializeCore(ReadOnlyDictionary<string, string> settings)
    {
        if (!settings.TryGetValue(RabbitMQSettings.UserName, out string? username) ||
            string.IsNullOrEmpty(username))
        {
            logger.LogInformation("Using username from connectionstring");
            username = connectionConfiguration.UserName;
            Diagnostics.AppendLine(
                $"Username not set, defaulted to using \"{username}\" username from the ConnectionString used by instance");
        }
        else
        {
            Diagnostics.AppendLine($"Username set to \"{username}\"");
        }

        if (!settings.TryGetValue(RabbitMQSettings.Password, out string? password) ||
            string.IsNullOrEmpty(password))
        {
            logger.LogInformation("Using password from connectionstring");
            password = connectionConfiguration.Password;
            Diagnostics.AppendLine(
                "Password not set, defaulted to using password from the ConnectionString used by instance");
        }
        else
        {
            Diagnostics.AppendLine("Password set");
        }

        var defaultCredential = new NetworkCredential(username, password);

        if (!settings.TryGetValue(RabbitMQSettings.API, out string? apiUrl) ||
            string.IsNullOrEmpty(apiUrl))
        {
            apiUrl =
                $"{(connectionConfiguration.UseTls ? $"https://{connectionConfiguration.Host}:15671" : $"http://{connectionConfiguration.Host}:15672")}";
            Diagnostics.AppendLine(
                $"RabbitMQ API Url not set, defaulted to using \"{apiUrl}\" from the ConnectionString used by instance");
        }
        else
        {
            Diagnostics.AppendLine($"RabbitMQ API Url set to \"{apiUrl}\"");
            if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out _))
            {
                InitialiseErrors.Add("API url configured is invalid");
            }
        }

        if (InitialiseErrors.Count == 0)
        {
            // ideally we would use the HttpClientFactory, but it would be a bit more involved to set that up
            // so for now we are using a virtual method that can be overriden in tests
            // https://github.com/Particular/ServiceControl/issues/4493
            httpClient = CreateHttpClient(defaultCredential, apiUrl);
            var authToken = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{username}:{password}"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
        }
    }

    protected virtual HttpClient CreateHttpClient(NetworkCredential defaultCredential, string apiUrl) =>
        new(new SocketsHttpHandler
        {
            Credentials = defaultCredential,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        })
        { BaseAddress = new Uri(apiUrl) };

    public override async IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IBrokerQueue brokerQueue,
        DateOnly startDate,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var queue = (RabbitMQBrokerQueueDetails)brokerQueue;
        var url = $"/api/queues/{HttpUtility.UrlEncode(queue.VHost)}/{HttpUtility.UrlEncode(queue.QueueName)}";

        logger.LogDebug($"Querying {url}");
        var newReading = await pipeline.ExecuteAsync(async token => new RabbitMQBrokerQueueDetails(await httpClient!.GetFromJsonAsync<JsonElement>(url, token)), cancellationToken);
        _ = queue.CalculateThroughputFrom(newReading);

        // looping for 24hrs, in 4 increments of 15 minutes
        for (var i = 0; i < 24 * 4; i++)
        {
            await Task.Delay(TimeSpan.FromMinutes(15), timeProvider, cancellationToken);
            logger.LogDebug($"Querying {url}");
            newReading = await pipeline.ExecuteAsync(async token => new RabbitMQBrokerQueueDetails(await httpClient!.GetFromJsonAsync<JsonElement>(url, token)), cancellationToken);

            var newTotalThroughput = queue.CalculateThroughputFrom(newReading);
            yield return new QueueThroughput
            {
                DateUTC = DateOnly.FromDateTime(timeProvider.GetUtcNow().DateTime),
                TotalThroughput = newTotalThroughput
            };
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

        var rabbitVersion = obj["rabbitmq_version"] ?? obj["product_version"] ?? obj["lavinmq_version"];
        var mgmtVersion = obj["management_version"];

        return (rabbitVersion?.GetValue<string>() ?? "Unknown", mgmtVersion?.GetValue<string>() ?? "Unknown");
    }

    public override async IAsyncEnumerable<IBrokerQueue> GetQueueNames(
        [EnumeratorCancellation] CancellationToken cancellationToken)
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

    public async Task<(RabbitMQBrokerQueueDetails[]?, bool morePages)> GetPage(int page, CancellationToken cancellationToken)
    {
        var url = $"/api/queues/{HttpUtility.UrlEncode(connectionConfiguration.VirtualHost)}?page={page}&page_size=500&name=&use_regex=false&pagination=true";

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

                    return (MaterializeQueueDetails(items), pageCount > pageReturned);
                }
            // Older versions of RabbitMQ API did not have paging and returned the array of items directly
            case JsonArray arr:
                {
                    return (MaterializeQueueDetails(arr), false);
                }
            default:
                throw new Exception("Was not able to get list of queues from RabbitMQ broker.");
        }
    }

    static RabbitMQBrokerQueueDetails[] MaterializeQueueDetails(JsonArray items)
    {
        // It is not possible to directly operated on the JsonNode. When the JsonNode is a JObject
        // and the indexer is access the internal dictionary is initialized which can cause key not found exceptions
        // when the payload contains the same key multiple times (which happened in the past).
        var queues = items.Select(item => new RabbitMQBrokerQueueDetails(item!.Deserialize<JsonElement>())).ToArray();
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

