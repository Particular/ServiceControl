namespace Particular.ThroughputCollector.Broker;

using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using Amazon;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.SQS;
using Amazon.SQS.Model;
using Shared;

public class AmazonSQSQuery : IThroughputQuery, IBrokerInfo
{
    private AmazonCloudWatchClient? cloudWatch;
    private AmazonSQSClient? sqs;
    private string? prefix;

    public void Initialise(FrozenDictionary<string, string> settings)
    {
        AWSCredentials credentials = new EnvironmentVariablesAWSCredentials();
        if (settings.TryGetValue(AmazonSQSSettings.Profile, out var profile))
        {
            var credentialsFile = new NetSDKCredentialsFile();
            if (credentialsFile.TryGetProfile(profile, out var credentialProfile))
            {
                if (credentialProfile.CanCreateAWSCredentials)
                {
                    credentials = credentialProfile.GetAWSCredentials(credentialProfile.CredentialProfileStore);
                }
            }
        }
        else
        {
            settings.TryGetValue(AmazonSQSSettings.AccessKey, out var accessKey);
            settings.TryGetValue(AmazonSQSSettings.SecretKey, out var secretKey);
            if (accessKey != null && secretKey != null)
            {
                credentials = new BasicAWSCredentials(accessKey, secretKey);
            }
            else
            {
                try
                {
                    credentials = new EnvironmentVariablesAWSCredentials();
                }
                catch (InvalidOperationException)
                { }
            }
        }

        RegionEndpoint? regionEndpoint = null;
        if (settings.TryGetValue(AmazonSQSSettings.Region, out var region))
        {
            regionEndpoint = RegionEndpoint.GetBySystemName(region);
        }

        sqs = new AmazonSQSClient(credentials, new AmazonSQSConfig { RegionEndpoint = regionEndpoint, RetryMode = RequestRetryMode.Adaptive, HttpClientFactory = new AwsHttpClientFactory() });
        cloudWatch = new AmazonCloudWatchClient(credentials, new AmazonCloudWatchConfig { RegionEndpoint = regionEndpoint, RetryMode = RequestRetryMode.Adaptive, HttpClientFactory = new AwsHttpClientFactory() });

        settings.TryGetValue(AmazonSQSSettings.Prefix, out prefix);
    }

    private class AwsHttpClientFactory : HttpClientFactory
    {
        public override HttpClient CreateHttpClient(IClientConfig clientConfig) => new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) });
        public override bool DisposeHttpClientsAfterUse(IClientConfig clientConfig) => false;
    }

    public async IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IQueueName queueName, DateOnly startDate,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        if (endDate <= startDate)
        {
            yield break;
        }

        var req = new GetMetricStatisticsRequest
        {
            Namespace = "AWS/SQS",
            MetricName = "NumberOfMessagesDeleted",
            StartTimeUtc = startDate.ToDateTime(TimeOnly.MinValue),
            EndTimeUtc = endDate.ToDateTime(TimeOnly.MinValue),
            Period = 86400, // 1 day
            Statistics = ["Sum"],
            Dimensions = [
                new Dimension { Name = "QueueName", Value = queueName.QueueName }
            ]
        };

        var resp = await cloudWatch!.GetMetricStatisticsAsync(req, cancellationToken);

        foreach (var datapoint in resp.Datapoints)
        {
            yield return new QueueThroughput
            {
                TotalThroughput = (long)datapoint.Sum,
                DateUTC = DateOnly.FromDateTime(datapoint.Timestamp.ToUniversalTime())
            };
        }
    }

    public async IAsyncEnumerable<IQueueName> GetQueueNames([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new ListQueuesRequest
        {
            MaxResults = 1000,
            QueueNamePrefix = prefix
        };

        while (!cancellationToken.IsCancellationRequested)
        {
            var response = await sqs!.ListQueuesAsync(request, cancellationToken);

            foreach (var queue in response.QueueUrls.Select(url => url.Split('/')[4]))
            {
                yield return new DefaultQueueName(queue);
            }

            if (response.NextToken is not null)
            {
                request.NextToken = response.NextToken;
            }
            else
            {
                break;
            }
        }
    }

    public string? ScopeType { get; }

    public bool SupportsHistoricalMetrics => true;
    public Dictionary<string, string> Data { get; } = [];
    public string MessageTransport => "AmazonSQS";
}