#nullable enable
namespace ServiceControl.Transports.SQS;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.SQS;
using Amazon.SQS.Model;
using BrokerThroughput;
using Microsoft.Extensions.Logging;

public class AmazonSQSQuery(ILogger<AmazonSQSQuery> logger, TimeProvider timeProvider, TransportSettings transportSettings)
    : BrokerThroughputQuery(logger, "AmazonSQS")
{
    AmazonCloudWatchClient? cloudWatch;
    AmazonSQSClient? sqs;
    string? prefix;

    protected override void InitialiseCore(FrozenDictionary<string, string> settings)
    {
        var sqsConnectionString = new SQSTransportConnectionString(transportSettings.ConnectionString);
        AWSCredentials credentials = FallbackCredentialsFactory.GetCredentials();
        RegionEndpoint? regionEndpoint = null;
        if (settings.TryGetValue(AmazonSQSSettings.Profile, out string? profile))
        {
            Diagnostics.Append($"Profile set to {profile}");
            var credentialsFile = new NetSDKCredentialsFile();
            if (credentialsFile.TryGetProfile(profile, out CredentialProfile? credentialProfile))
            {
                if (credentialProfile.CanCreateAWSCredentials)
                {
                    credentials = credentialProfile.GetAWSCredentials(credentialProfile.CredentialProfileStore);
                    logger.LogInformation($"Using credentials set in '{profile}' profile");
                    Diagnostics.AppendLine(", using profile credentials");
                }

                logger.LogInformation($"Using region set in '{profile}' profile");
                regionEndpoint = new ProfileAWSRegion(credentialsFile, profile).Region;
            }
            else
            {
                Diagnostics.AppendLine($"Profile set to \"{profile}\"");
            }
        }
        else
        {
            Diagnostics.AppendLine("Profile not set");

            if (settings.TryGetValue(AmazonSQSSettings.AccessKey, out string? accessKey))
            {
                Diagnostics.AppendLine("AccessKey set");
            }

            if (settings.TryGetValue(AmazonSQSSettings.SecretKey, out string? secretKey))
            {
                Diagnostics.AppendLine("SecretKey set");
            }

            if (accessKey != null && secretKey != null)
            {
                logger.LogInformation("Using basic credentials");
                credentials = new BasicAWSCredentials(accessKey, secretKey);
            }
            else
            {
                if (sqsConnectionString is { AccessKey: not null, SecretKey: not null })
                {
                    credentials = new BasicAWSCredentials(sqsConnectionString.AccessKey, sqsConnectionString.SecretKey);
                    Diagnostics.AppendLine("AccessKey not set, defaulted to using the value from the ConnectionString used by instance");
                    Diagnostics.AppendLine("SecretKey not set, defaulted to using the value from the ConnectionString used by instance");
                    logger.LogInformation("Using AccessKey and SecretKey used by the instance ConnectionString");
                }
                else
                {
                    Diagnostics.AppendLine(
                        "AccessKey not set, attempting to use existing environment variable (AWS_ACCESS_KEY_ID) or IAM role credentials");
                    Diagnostics.AppendLine(
                        "SecretKey not set, attempting to use existing environment variable (AWS_SECRET_ACCESS_KEY) or IAM role credentials");
                    logger.LogInformation("Attempting to use existing environment variables or IAM role credentials");
                }
            }
        }

        if (settings.TryGetValue(AmazonSQSSettings.Region, out string? region))
        {
            string? previousSetSystemName = regionEndpoint?.SystemName;
            regionEndpoint = RegionEndpoint.GetBySystemName(region);

            Diagnostics.Append($"Region set to \"{regionEndpoint.SystemName}\"");
            if (previousSetSystemName != regionEndpoint.SystemName)
            {
                Diagnostics.Append(", this setting overrides the profile setting");
            }

            Diagnostics.AppendLine();
        }
        else if (regionEndpoint == null)
        {
            if (sqsConnectionString.Region != null)
            {
                regionEndpoint = RegionEndpoint.GetBySystemName(sqsConnectionString.Region);
                Diagnostics.AppendLine(
                    $"Region not set, defaulted to using \"{regionEndpoint.SystemName}\" from the ConnectionString used by instance");
                logger.LogInformation("Using Region used by the instance ConnectionString");
            }
            else
            {
                logger.LogInformation("Attempting to use AWS environment variable for region");
                try
                {
                    regionEndpoint = new EnvironmentVariableAWSRegion().Region;
                    Diagnostics.AppendLine(
                        $"Region not set, using \"{regionEndpoint.SystemName}\" set by the environment setting (AWS_REGION)");
                }
                catch (InvalidOperationException)
                {
                    Diagnostics.AppendLine("Region not set");
                    throw;
                }
            }
        }

        sqs = new AmazonSQSClient(credentials,
            new AmazonSQSConfig
            {
                RegionEndpoint = regionEndpoint,
                RetryMode = RequestRetryMode.Adaptive,
                HttpClientFactory = new AwsHttpClientFactory()
            });
        cloudWatch = new AmazonCloudWatchClient(credentials,
            new AmazonCloudWatchConfig
            {
                RegionEndpoint = regionEndpoint,
                RetryMode = RequestRetryMode.Adaptive,
                HttpClientFactory = new AwsHttpClientFactory()
            });

        settings.TryGetValue(AmazonSQSSettings.Prefix, out prefix);
    }

    public static class AmazonSQSSettings
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
        readonly HttpClient httpClient =
            new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) });

        public override HttpClient CreateHttpClient(IClientConfig clientConfig) => httpClient;
        public override bool DisposeHttpClientsAfterUse(IClientConfig clientConfig) => false;
    }

    public override async IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IBrokerQueue brokerQueue,
        DateOnly startDate,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var endDate = DateOnly.FromDateTime(timeProvider.GetUtcNow().DateTime).AddDays(-1);

        if (endDate < startDate)
        {
            yield break;
        }

        var req = new GetMetricStatisticsRequest
        {
            Namespace = "AWS/SQS",
            MetricName = "NumberOfMessagesDeleted",
            StartTimeUtc = startDate.ToDateTime(TimeOnly.MinValue),
            EndTimeUtc = endDate.ToDateTime(TimeOnly.MaxValue),
            Period = 24 * 60 * 60, // 1 day
            Statistics = ["Sum"],
            Dimensions = [
                new Dimension { Name = "QueueName", Value = brokerQueue.QueueName }
            ]
        };

        var resp = await cloudWatch!.GetMetricStatisticsAsync(req, cancellationToken);

        DateOnly currentDate = startDate;
        var data = new Dictionary<DateOnly, QueueThroughput>();
        while (currentDate <= endDate)
        {
            data.Add(currentDate, new QueueThroughput { TotalThroughput = 0, DateUTC = currentDate });

            currentDate = currentDate.AddDays(1);
        }

        foreach (var datapoint in resp.Datapoints)
        {
            data[DateOnly.FromDateTime(datapoint.Timestamp.ToUniversalTime())].TotalThroughput = (long)datapoint.Sum;
        }

        foreach (QueueThroughput queueThroughput in data.Values)
        {
            yield return queueThroughput;
        }
    }

    public override async IAsyncEnumerable<IBrokerQueue> GetQueueNames(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
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
                yield return new DefaultBrokerQueue(queue);
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

    public override KeyDescriptionPair[] Settings =>
    [
        new KeyDescriptionPair(AmazonSQSSettings.AccessKey, AmazonSQSSettings.AccessKeyDescription),
        new KeyDescriptionPair(AmazonSQSSettings.SecretKey, AmazonSQSSettings.SecretKeyDescription),
        new KeyDescriptionPair(AmazonSQSSettings.Profile, AmazonSQSSettings.ProfileDescription),
        new KeyDescriptionPair(AmazonSQSSettings.Prefix, AmazonSQSSettings.PrefixDescription),
        new KeyDescriptionPair(AmazonSQSSettings.Region, AmazonSQSSettings.RegionDescription)
    ];

    protected override async Task<(bool Success, List<string> Errors)> TestConnectionCore(
        CancellationToken cancellationToken)
    {
        await foreach (IBrokerQueue brokerQueue in GetQueueNames(cancellationToken))
        {
            // Just picking 10 days ago to test the connection
            await foreach (QueueThroughput _ in GetThroughputPerDay(brokerQueue,
                               DateOnly.FromDateTime(timeProvider.GetUtcNow().DateTime).AddDays(-10),
                               cancellationToken))
            {
                return (true, []);
            }
        }

        return (true, []);
    }

    public override string SanitizeEndpointName(string endpointName)
    {
        try
        {
            return QueueNameHelper.GetSqsQueueName(endpointName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Failed to sanitize endpoint name {endpointName}");
            return endpointName;
        }
    }
}