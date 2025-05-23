namespace ServiceControl.Transports.SQS;

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using NServiceBus.Logging;
using NServiceBus.Transport;

public class RegexErrorQueueDiscoveryMethod : IErrorQueueDiscoveryMethod
{
    public string Name => "Regex";

    public string Pattern { get; set; } = "^(.*)_error$";

    public RegexErrorQueueDiscoveryMethod(TransportSettings settings)
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

    public Func<(MessageContext Context, string ErrorQueueName), string> GetReturnQueueName => faultInfo =>
    {
        regex ??= new Regex(Pattern, RegexOptions.Compiled);

        var match = regex.Match(faultInfo.ErrorQueueName);

        if (match.Success)
        {
            if (match.Groups.Count != 1)
            {
                throw new Exception($"The regex pattern '{Pattern}' must have exactly one capture group that will match the return queue name");
            }
            return match.Groups[1].Value;
        }
        else
        {
            throw new Exception("The regex pattern did not match the error queue name");
        }
    };

    public async Task<IEnumerable<string>> GetErrorQueueNames(CancellationToken cancellationToken = default)
    {
        using var client = clientFactory();

        var request = new ListQueuesRequest();

        if (!string.IsNullOrEmpty(queueNamePrefix))
        {
            request.QueueNamePrefix = queueNamePrefix;
        }

        var response = await client.ListQueuesAsync(request, cancellationToken);

        var errorQueueNames = new List<string>();

        foreach (var queueUrl in response.QueueUrls)
        {
            var queueName = queueUrl.Substring(queueUrl.LastIndexOf('/') + 1);

            regex ??= new Regex(Pattern, RegexOptions.Compiled);

            if (regex.IsMatch(queueName))
            {
                errorQueueNames.Add(queueName);
            }
        }

        return errorQueueNames;
    }

    static Regex regex;

    readonly string queueNamePrefix;
    readonly Func<IAmazonSQS> clientFactory = () => new AmazonSQSClient();

    static readonly ILog Logger = LogManager.GetLogger<RegexErrorQueueDiscoveryMethod>();
}
