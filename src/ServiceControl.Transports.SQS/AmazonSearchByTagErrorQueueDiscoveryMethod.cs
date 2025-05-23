namespace ServiceControl.Transports.SQS;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using NServiceBus.Logging;
using NServiceBus.Transport;

public class AmazonSearchByTagErrorQueueDiscoveryMethod : IErrorQueueDiscoveryMethod
{
    public string Name => "AmazonSearchByTag";

    public Func<(MessageContext Context, string ErrorQueueName), string> GetReturnQueueName => faultInfo => tagCache[faultInfo.ErrorQueueName];

    public string TagKey { get; set; } = "ServiceControlErrorQueue";

    public AmazonSearchByTagErrorQueueDiscoveryMethod(TransportSettings settings)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = settings.ConnectionString };
        if (builder.ContainsKey("AccessKeyId") || builder.ContainsKey("SecretAccessKey"))
        {
            // if the user provided the access key and secret access key they should always be loaded from environment credentials
            clientFactory = () => new AmazonSQSClient(new EnvironmentVariablesAWSCredentials());
        }
        else
        {
            //See https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/net-dg-config-creds.html#creds-assign
            Logger.Info("BasicAWSCredentials have not been supplied in the connection string. Attempting to use existing environment or IAM role credentials.");
        }

        if (builder.TryGetValue("QueueNamePrefix", out var prefix))
        {
            queueNamePrefix = (string)prefix;
        }
    }

    public async Task<IEnumerable<string>> GetErrorQueueNames(CancellationToken cancellationToken = default)
    {
        using var client = clientFactory();

        var request = new ListQueuesRequest();

        if (!string.IsNullOrEmpty(queueNamePrefix))
        {
            request.QueueNamePrefix = queueNamePrefix;
        }

        Logger.Debug($"Requesting list of queues with prefix '{request.QueueNamePrefix}'");

        var response = await client.ListQueuesAsync(request, cancellationToken);

        var isErrorQueueTasks = new List<Task<(string QueueName, string TagValue)>>();

        foreach (var queueUrl in response.QueueUrls)
        {
            isErrorQueueTasks.Add(IsErrorQueue(client, queueUrl, cancellationToken));
        }

        var results = await Task.WhenAll(isErrorQueueTasks);

        var validQueues = results.Where(r => r.QueueName is not null).ToList();

        tagCache = new ReadOnlyDictionary<string, string>(validQueues.ToDictionary(r => r.QueueName, r => r.TagValue));

        return validQueues.Select(r => r.QueueName);
    }

    async Task<(string QueueName, string TagValue)> IsErrorQueue(IAmazonSQS client, string queueUrl, CancellationToken cancellationToken)
    {
        var queueName = queueUrl.Substring(queueUrl.LastIndexOf('/') + 1);

        Logger.Debug($"Checking queue '{queueName}' for tag '{TagKey}'");

        var tagResponse = await client.ListQueueTagsAsync(new ListQueueTagsRequest
        {
            QueueUrl = queueUrl
        }, cancellationToken);

        if (tagResponse.Tags.TryGetValue(TagKey, out var value))
        {
            Logger.Debug($"Found tag '{TagKey}' with value '{value}' on queue '{queueName}'");
            return (queueUrl, value);
        }

        return (null, null);
    }

    readonly string queueNamePrefix;
    readonly Func<IAmazonSQS> clientFactory = () => new AmazonSQSClient();

    static ReadOnlyDictionary<string, string> tagCache;

    static readonly ILog Logger = LogManager.GetLogger<AmazonSearchByTagErrorQueueDiscoveryMethod>();
}