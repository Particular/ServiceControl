using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using JustSaying.Messaging.Middleware;
using Microsoft.Extensions.Logging;

namespace Particular.JustSaying.RetryMiddleware;

public class FaultMetadataMiddleware(IAmazonSQS sqs, ILogger logger) : MiddlewareBase<HandleMessageContext, bool>
{
    protected async override Task<bool> RunInnerAsync(HandleMessageContext context, Func<CancellationToken, Task<bool>> func, CancellationToken stoppingToken)
    {
        // var url = await sqsClient.GetQueueUrlAsync("JustSaying_exceptions");
        var successful = await func(stoppingToken).ConfigureAwait(false);

        if (successful)
        {
            return successful;
        }

        var nserviceBusHeaders = new Dictionary<string, string>();

        nserviceBusHeaders["NserviceBus.MessageId"] = context.Message.Id.ToString();
        if (context.HandledException == null)
        {
            nserviceBusHeaders["NServiceBus.ExceptionInfo.ExceptionType"] = context.HandledException.GetType().FullName;
            nserviceBusHeaders["NServiceBus.ExceptionInfo.StackTrace"] = context.HandledException.ToString();
        }

        //nserviceBusHeaders[""];
        var exception = JsonSerializer.Serialize(nserviceBusHeaders);

        var request = new SendMessageRequest
        {
            QueueUrl = "",
            MessageBody = exception
        };
        sqs.SendMessageAsync(request);

        // TODO - just returning this so things will build
        return false;
    }
}