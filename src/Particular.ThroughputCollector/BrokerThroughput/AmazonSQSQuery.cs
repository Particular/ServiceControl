namespace Particular.ThroughputCollector.Broker;

using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using System.Threading.RateLimiting;
using Amazon;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.SQS;
using Amazon.SQS.Model;
using Persistence;
using Shared;

public class AmazonSQSQuery : IThroughputQuery
{
    private AmazonCloudWatchClient? cloudWatch;
    private AmazonSQSClient? sqs;
    private readonly FixedWindowRateLimiter rateLimiter = new(new FixedWindowRateLimiterOptions
    {
        AutoReplenishment = true,
        // 1/4 the AWS default quota value (400) for cloudwatch, still do 20k queues in 3 minutes
        PermitLimit = 100,
        Window = TimeSpan.FromSeconds(1),
        // Otherwise AcquireAsync() will return a lease immediately with IsAcquired = false
        QueueLimit = int.MaxValue
    });
    private string? prefix;

    public void Initialise(FrozenDictionary<string, string> settings)
    {
        AWSCredentials credentials = new EnvironmentVariablesAWSCredentials();
        if (settings.TryGetValue(AmazonSQSSettings.Profile, out string? profile))
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
            credentials = new BasicAWSCredentials(settings[AmazonSQSSettings.AccessKey], settings[AmazonSQSSettings.SecretKey]);
        }

        RegionEndpoint? regionEndpoint = null;
        if (settings.TryGetValue(AmazonSQSSettings.Region, out string? region))
        {
            regionEndpoint = RegionEndpoint.GetBySystemName(region);
        }

        sqs = new AmazonSQSClient(credentials, regionEndpoint);
        cloudWatch = new AmazonCloudWatchClient(credentials, regionEndpoint);

        settings.TryGetValue(AmazonSQSSettings.Prefix, out prefix);
    }

    public async IAsyncEnumerable<EndpointThroughput> GetThroughputPerDay(IQueueName queueName, DateTime startDate,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var endDate = DateTime.UtcNow.Date.AddDays(-1);
        var req = new GetMetricStatisticsRequest
        {
            Namespace = "AWS/SQS",
            MetricName = "NumberOfMessagesDeleted",
            StartTimeUtc = startDate,
            EndTimeUtc = endDate,
            Period = 86400, // 1 day
            Statistics = ["Sum"],
            Dimensions = [
                new Dimension { Name = "QueueName", Value = queueName.QueueName }
            ]
        };

        using var lease = await rateLimiter.AcquireAsync(cancellationToken: cancellationToken);
        var resp = await cloudWatch!.GetMetricStatisticsAsync(req, cancellationToken);

        foreach (var datapoint in resp.Datapoints)
        {
            yield return new EndpointThroughput
            {
                TotalThroughput = (long)datapoint.Sum,
                DateUTC = datapoint.Timestamp.ToUniversalTime().Date
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

            foreach (string queue in response.QueueUrls.Select(url => url.Split('/')[4]))
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
}