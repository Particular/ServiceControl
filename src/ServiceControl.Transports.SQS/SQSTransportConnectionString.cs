namespace ServiceControl.Transports.SQS;

using System;
using System.Data.Common;

public class SQSTransportConnectionString
{
    public SQSTransportConnectionString(string connectionString)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

        if (builder.TryGetValue("AccessKeyId", out object accessKeyId))
        {
            AccessKey = (string)accessKeyId;
        }

        if (builder.TryGetValue("SecretAccessKey", out object secretAccessKey))
        {
            SecretKey = (string)secretAccessKey;
        }

        if (builder.TryGetValue("Region", out object region))
        {
            Region = (string)region;
        }

        if (builder.TryGetValue("QueueNamePrefix", out object queueNamePrefix))
        {
            QueueNamePrefix = (string)queueNamePrefix;
        }

        if (builder.TryGetValue("TopicNamePrefix", out object topicNamePrefix))
        {
            TopicNamePrefix = (string)topicNamePrefix;
        }

        if (builder.TryGetValue("S3BucketForLargeMessages", out object bucketForLargeMessages))
        {
            S3BucketForLargeMessages = (string)bucketForLargeMessages;
            if (!string.IsNullOrEmpty(S3BucketForLargeMessages))
            {
                if (builder.TryGetValue("S3KeyPrefix", out object keyPrefix))
                {
                    S3KeyPrefix = (string)keyPrefix;
                }
            }
        }

        if (builder.TryGetValue("DoNotWrapOutgoingMessages", out object doNotWrapOutgoingMessages) &&
            bool.TryParse(doNotWrapOutgoingMessages.ToString(), out bool doNotWrapOutgoingMessagesAsBool))
        {
            DoNotWrapOutgoingMessages = doNotWrapOutgoingMessagesAsBool;
        }


        if (builder.TryGetValue("ReservedBytes", out object reservedBytes))
        {
            ReservedBytesInMessageSize = Convert.ToInt32(reservedBytes);
        }
    }

    public string AccessKey { get; }
    public string SecretKey { get; }
    public string Region { get; }
    public string QueueNamePrefix { get; }
    public string TopicNamePrefix { get; }
    public string S3BucketForLargeMessages { get; }
    public string S3KeyPrefix { get; }
    public bool DoNotWrapOutgoingMessages { get; }
    public int ReservedBytesInMessageSize { get; }
}