namespace ServiceControl.Transports.SQS
{
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.SQS;
    using Amazon.SQS.Model;

    class QueueAttributesRequestCache(IAmazonSQS sqsClient)
    {
        public async Task<GetQueueAttributesRequest> GetQueueAttributesRequest(string queueName, CancellationToken cancellationToken = default)
        {
            if (cache.TryGetValue(queueName, out var attReq))
            {
                return attReq;
            }

            var queueUrl = await GetQueueUrl(queueName, cancellationToken);

            attReq = new GetQueueAttributesRequest
            {
                QueueUrl = queueUrl,
                AttributeNames = ["ApproximateNumberOfMessages"]
            };

            cache[queueName] = attReq;

            return attReq;
        }

        async Task<string> GetQueueUrl(string queueName, CancellationToken cancellationToken)
        {
            var response = await sqsClient.GetQueueUrlAsync(queueName, cancellationToken);
            return response.QueueUrl;
        }

        readonly ConcurrentDictionary<string, GetQueueAttributesRequest> cache = new();
    }
}