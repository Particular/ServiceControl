namespace ServiceControl.Transports.SQS
{
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.SQS;
    using Amazon.SQS.Model;

    class QueueAttributesRequestCache
    {
        public QueueAttributesRequestCache(IAmazonSQS sqsClient)
        {
            cache = new ConcurrentDictionary<string, GetQueueAttributesRequest>();
            this.sqsClient = sqsClient;
        }

        public async Task<GetQueueAttributesRequest> GetQueueAttributesRequest(string queueName, CancellationToken cancellationToken = default)
        {
            if (cache.TryGetValue(queueName, out var attReq))
            {
                return attReq;
            }

            var queueUrl = await GetQueueUrl(queueName, cancellationToken).ConfigureAwait(false);

            attReq = new GetQueueAttributesRequest { QueueUrl = queueUrl };
            attReq.AttributeNames.Add("ApproximateNumberOfMessages");

            cache[queueName] = attReq;

            return attReq;
        }

        async Task<string> GetQueueUrl(string queueName, CancellationToken cancellationToken)
        {
            var response = await sqsClient.GetQueueUrlAsync(queueName, cancellationToken)
                .ConfigureAwait(false);
            return response.QueueUrl;
        }

        ConcurrentDictionary<string, GetQueueAttributesRequest> cache;
        IAmazonSQS sqsClient;
    }
}