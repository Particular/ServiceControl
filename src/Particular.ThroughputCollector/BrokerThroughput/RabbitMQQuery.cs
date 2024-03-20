namespace Particular.ThroughputCollector.Broker;

using System.Collections.Frozen;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Web;
using ServiceControl.Transports;
using ServiceControl.Transports.RabbitMQ;
using Shared;

public class RabbitMQQuery : IThroughputQuery, IBrokerInfo
{
    private HttpClient? httpClient;

    public void Initialise(FrozenDictionary<string, string> settings)
    {
        var connectionString = settings[CommonSettings.TransportConnectionString];
        var connectionConfiguration = RabbitMQConnectionConfiguration.Create(connectionString, "");

        if (!settings.TryGetValue(RabbitMQSettings.UserName, out var username))
        {
            username = connectionConfiguration.UserName;
        }

        if (!settings.TryGetValue(RabbitMQSettings.Password, out var password))
        {
            password = connectionConfiguration.UserName;
        }

        var defaultCredential = new NetworkCredential(username, password);

        if (!settings.TryGetValue(RabbitMQSettings.API, out var apiUrl))
        {
            apiUrl = $"{(connectionConfiguration.UseTls ? "https://" : "http://")}{connectionConfiguration.Host}:{connectionConfiguration.Port}";
        }

        httpClient = new HttpClient(new SocketsHttpHandler
        {
            Credentials = defaultCredential,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        })
        {
            BaseAddress = new Uri(apiUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public async IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IQueueName queueName, DateOnly startDate,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var queue = (RabbitMQQueueDetails)queueName;
        var url = $"/api/queues/{HttpUtility.UrlEncode(queue.VHost)}/{HttpUtility.UrlEncode(queue.QueueName)}";

        var token = await httpClient!.GetFromJsonAsync<JsonNode>(url, cancellationToken);
        queue.AckedMessages = GetAck();

        // looping for 24hrs, in 4 increments of 15 minutes
        for (var i = 0; i < 24 * 4; i++)
        {
            await Task.Delay(TimeSpan.FromMinutes(15), cancellationToken);

            token = await httpClient!.GetFromJsonAsync<JsonNode>(url, cancellationToken);
            var newReading = GetAck();
            if (newReading is not null)
            {
                if (newReading > queue.AckedMessages)
                {
                    yield return new QueueThroughput
                    {
                        DateUTC = DateOnly.FromDateTime(DateTime.UtcNow),
                        TotalThroughput = newReading.Value - queue.AckedMessages.Value,
                        Scope = queue.VHost
                    };
                }
                queue.AckedMessages = newReading;
            }
        }
        yield break;

        long? GetAck()
        {
            if (token!["message_stats"] is JsonObject stats && stats["ack"] is JsonValue val)
            {
                return val.GetValue<long>();
            }
            return null;
        }
    }

    public async Task<(string rabbitVersion, string managementVersion)> GetRabbitDetails(CancellationToken cancellationToken = default)
    {
        var overviewUrl = "/api/overview";

        var obj = await httpClient!.GetFromJsonAsync<JsonObject>(overviewUrl, cancellationToken) ?? throw new QueryException(QueryFailureReason.InvalidEnvironment, "The RabbitMQ broker is configured with `management.disable_stats = true` or `management_agent.disable_metrics_collector = true` and as a result queue statistics cannot be collected using this tool. Consider changing the configuration of the RabbitMQ broker.");
        var statsDisabled = obj["disable_stats"]?.GetValue<bool>() ?? false;

        if (statsDisabled)
        {
            throw new QueryException(QueryFailureReason.InvalidEnvironment, "The RabbitMQ broker is configured with `management.disable_stats = true` or `management_agent.disable_metrics_collector = true` and as a result queue statistics cannot be collected using this tool. Consider changing the configuration of the RabbitMQ broker.");
        }

        var rabbitVersion = obj["rabbitmq_version"] ?? obj["product_version"];
        var mgmtVersion = obj["management_version"];

        return (rabbitVersion?.GetValue<string>() ?? "Unknown", mgmtVersion?.GetValue<string>() ?? "Unknown");
    }

    public async IAsyncEnumerable<IQueueName> GetQueueNames([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var page = 1;
        bool morePages;
        var vHosts = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

        (var rabbitVersion, var managementVersion) = await GetRabbitDetails(cancellationToken);
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

    private async Task AddAdditionalQueueDetails(RabbitMQQueueDetails queue, CancellationToken cancellationToken = default)
    {
        try
        {
            var bindingsUrl = $"/api/queues/{HttpUtility.UrlEncode(queue.VHost)}/{HttpUtility.UrlEncode(queue.QueueName)}/bindings";
            var bindings = await httpClient!.GetFromJsonAsync<JsonArray>(bindingsUrl, cancellationToken);
            var conventionalBindingFound = bindings?.Any(binding => binding!["source"]?.GetValue<string>() == queue.QueueName
                                                                    && binding["vhost"]?.GetValue<string>() == queue.VHost
                                                                    && binding["destination"]?.GetValue<string>() == queue.QueueName
                                                                    && binding["destination_type"]?.GetValue<string>() == "queue"
                                                                    && binding["routing_key"]?.GetValue<string>() == string.Empty
                                                                    && binding["properties_key"]?.GetValue<string>() == "~") ?? false;

            if (conventionalBindingFound)
            {
                queue.EndpointIndicators.Add("ConventionalTopologyBinding");
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Clearly no conventional topology binding here
        }

        try
        {
            var exchangeUrl = $"/api/exchanges/{HttpUtility.UrlEncode(queue.VHost)}/{HttpUtility.UrlEncode(queue.QueueName)}/bindings/destination";
            var bindings = await httpClient!.GetFromJsonAsync<JsonArray>(exchangeUrl, cancellationToken);
            var delayBindingFound = bindings?.Any(binding =>
            {
                var source = binding!["source"]?.GetValue<string>();

                return source is "nsb.v2.delay-delivery" or "nsb.delay-delivery"
                    && binding["vhost"]?.GetValue<string>() == queue.VHost
                    && binding["destination"]?.GetValue<string>() == queue.QueueName
                    && binding["destination_type"]?.GetValue<string>() == "exchange"
                    && binding["routing_key"]?.GetValue<string>() == $"#.{queue.QueueName}";
            }) ?? false;

            if (delayBindingFound)
            {
                queue.EndpointIndicators.Add("DelayBinding");
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Clearly no delay binding here
        }
    }

    private async Task<(RabbitMQQueueDetails[]?, bool morePages)> GetPage(int page, CancellationToken cancellationToken)
    {
        var url = $"/api/queues?page={page}&page_size=500&name=&use_regex=false&pagination=true";

        var container = await httpClient!.GetFromJsonAsync<JsonNode>(url, cancellationToken);
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

                    var queues = items.Select(item => new RabbitMQQueueDetails(item!)).ToArray();

                    return (queues, pageCount > pageReturned);
                }
            // Older versions of RabbitMQ API did not have paging and returned the array of items directly
            case JsonArray arr:
                {
                    var queues = arr.Select(item => new RabbitMQQueueDetails(item!)).ToArray();

                    return (queues, false);
                }
            default:
                throw new Exception("Was not able to get list of queues from RabbitMQ broker.");
        }
    }

    public string? ScopeType { get; set; }
    public bool SupportsHistoricalMetrics => false;
    public Dictionary<string, string> Data { get; } = [];
    public string MessageTransport => "SqlTransport";
}