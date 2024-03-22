#nullable enable
namespace ServiceControl.Transports.SQS;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using Amazon;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.SQS;
using Amazon.SQS.Model;

class AmazonSQSQuery(TimeProvider timeProvider) : IThroughputQuery
{
    AmazonCloudWatchClient? cloudWatch;
    AmazonSQSClient? sqs;
    string? prefix;

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

    static class AmazonSQSSettings
    {
        public static readonly string AccessKey = "AmazonSQS/AccessKey";

        public static readonly string AccessKeyDescription =
            "The AWS Access Key ID to use to discover queue names and gather per-queue metrics.";

        public static readonly string SecretKey = "AmazonSQS/SecretKey";

        public static readonly string SecretKeyDescription =
            "The AWS Secret Access Key to use to discover queue names and gather per-queue metrics.";

        public static readonly string Profile = "AmazonSQS/Profile";

        public static readonly string ProfileDescription =
            "The name of a local AWS credentials profile to use to discover queue names and gather per-queue metrics.";

        public static readonly string Region = "AmazonSQS/Region";
        public static readonly string RegionDescription = "The AWS region to use when accessing AWS services.";
        public static readonly string Prefix = "AmazonSQS/Prefix";

        public static readonly string PrefixDescription =
            "Report only on queues that begin with a specific prefix. This is commonly used when one AWS account must contain queues for multiple projects or multiple environments.";
    }

    class AwsHttpClientFactory : HttpClientFactory
    {
        public override HttpClient CreateHttpClient(IClientConfig clientConfig) => new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) });
        public override bool DisposeHttpClientsAfterUse(IClientConfig clientConfig) => false;
    }

    public async IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IQueueName queueName, DateOnly startDate,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var endDate = DateOnly.FromDateTime(timeProvider.GetUtcNow().DateTime).AddDays(-1);

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
            Period = 24 * 60 * 60, // 1 day
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

    public string? ScopeType => null;

    public bool SupportsHistoricalMetrics => true;

    public KeyDescriptionPair[] Settings =>
    [
        new KeyDescriptionPair(AmazonSQSSettings.AccessKey, AmazonSQSSettings.AccessKeyDescription),
        new KeyDescriptionPair(AmazonSQSSettings.SecretKey, AmazonSQSSettings.SecretKeyDescription),
        new KeyDescriptionPair(AmazonSQSSettings.Profile, AmazonSQSSettings.ProfileDescription),
        new KeyDescriptionPair(AmazonSQSSettings.Prefix, AmazonSQSSettings.PrefixDescription),
        new KeyDescriptionPair(AmazonSQSSettings.Region, AmazonSQSSettings.RegionDescription)
    ];
    public Dictionary<string, string> Data { get; } = [];
    public string MessageTransport => "AmazonSQS";
}