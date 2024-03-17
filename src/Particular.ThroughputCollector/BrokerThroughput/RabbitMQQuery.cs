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

public class RabbitMQQuery : IThroughputQuery
{
    private HttpClient httpClient = new();

    public void Initialise(FrozenDictionary<string, string> settings)
    {
        string connectionString = settings[CommonSettings.TransportConnectionString];
        var connectionConfiguration = RabbitMQConnectionConfiguration.Create(connectionString, "");

        if (!settings.TryGetValue(RabbitMQSettings.UserName, out string? username))
        {
            username = connectionConfiguration.UserName;
        }

        if (!settings.TryGetValue(RabbitMQSettings.Password, out string? password))
        {
            password = connectionConfiguration.UserName;
        }

        var defaultCredential = new NetworkCredential(username, password);

        if (!settings.TryGetValue(RabbitMQSettings.API, out string? apiUrl))
        {
            apiUrl = $"{(connectionConfiguration.UseTls ? "https://" : "http://")}{connectionConfiguration.Host}:{connectionConfiguration.Port}";
        }

        httpClient = new HttpClient(new HttpClientHandler
        {
            Credentials = defaultCredential
        })
        {
            BaseAddress = new Uri(apiUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public async IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IQueueName queueName, DateTime startDate,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var queue = (RabbitMQQueueDetails)queueName;
        string url = $"/api/queues/{HttpUtility.UrlEncode(queue.VHost)}/{HttpUtility.UrlEncode(queue.QueueName)}";

        var token = await httpClient.GetFromJsonAsync<JsonNode>(url, cancellationToken);
        queue.AckedMessages = GetAck();

        // looping for 24hrs, in 4 increments of 15 minutes
        for (int i = 0; i < 24 * 4; i++)
        {
            await Task.Delay(TimeSpan.FromMinutes(15), cancellationToken);

            token = await httpClient.GetFromJsonAsync<JsonNode>(url, cancellationToken);
            long? newReading = GetAck();
            if (newReading is not null)
            {
                if (newReading > queue.AckedMessages)
                {
                    yield return new QueueThroughput
                    {
                        DateUTC = DateTime.UtcNow.Date,
                        TotalThroughput = newReading.Value - queue.AckedMessages.Value
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

    public async IAsyncEnumerable<IQueueName> GetQueueNames([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int page = 1;
        bool morePages = true;

        while (morePages)
        {
            (var queues, morePages) = await GetPage(page, cancellationToken);

            if (queues != null)
            {
                foreach (var rabbitMQQueueDetails in queues)
                {
                    yield return rabbitMQQueueDetails;
                }
            }

            page++;
        }
    }

    private async Task<(RabbitMQQueueDetails[]?, bool morePages)> GetPage(int page, CancellationToken cancellationToken)
    {
        string url = $"/api/queues?page={page}&page_size=500&name=&use_regex=false&pagination=true";

        var container = await httpClient.GetFromJsonAsync<JsonNode>(url, cancellationToken);
        switch (container)
        {
            case JsonObject obj:
                {
                    int pageCount = obj["page_count"]!.GetValue<int>();
                    int pageReturned = obj["page"]!.GetValue<int>();

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
}