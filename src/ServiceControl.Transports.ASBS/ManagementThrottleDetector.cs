namespace ServiceControl.Transports.ASBS
{
    using System.Threading;
    using Azure.Core;
    using Azure.Core.Pipeline;

    // Counts the HTTP 429 throttling responses on the management client's pipeline, including the ones the SDK's
    // retry absorbs so they never throw. Attach at PerRetry so every attempt is seen.
    sealed class ManagementThrottleDetector : HttpPipelineSynchronousPolicy
    {
        // Interlocked: the pipeline runs on SDK I/O threads. The loop snapshots this around each query.
        public long ThrottledResponseCount => Interlocked.Read(ref throttledResponseCount);

        public override void OnReceivedResponse(HttpMessage message)
        {
            if (IsThrottleResponse(message.Response.Status))
            {
                Interlocked.Increment(ref throttledResponseCount);
            }
        }

        // HTTP 429 (Too Many Requests) is how the Service Bus management endpoint signals throttling.
        internal static bool IsThrottleResponse(int statusCode) => statusCode == 429;

        long throttledResponseCount;
    }
}
