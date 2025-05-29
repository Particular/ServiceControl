using Amazon.SQS;
using Amazon.SQS.Model;
using JustSaying.Messaging.Middleware;
using Microsoft.Extensions.Logging;
using NServiceBus;

namespace Particular.JustSaying.RetryMiddleware;

public sealed class RetryAcknowledgementMiddleware(IAmazonSQS sqs, ILogger logger) : MiddlewareBase<HandleMessageContext, bool>
{
    internal const string RetryUniqueMessageIdHeaderKey = "ServiceControl.Retry.UniqueMessageId";
    internal const string RetryConfirmationQueueHeaderKey = "ServiceControl.Retry.AcknowledgementQueue";

    protected override async Task<bool> RunInnerAsync(HandleMessageContext context, Func<CancellationToken, Task<bool>> next, CancellationToken stoppingToken)
    {
        try
        {
            var useRetryAcknowledgement = IsRetriedMessage(context, out var id, out var acknowledgementQueue);

            logger.LogInformation($"IsRetriedMessage: {useRetryAcknowledgement}, id: {id}, queue: {acknowledgementQueue}");

            var successful = await next(stoppingToken).ConfigureAwait(false);

            logger.LogInformation($"Handler executed. Success: {successful}");

            if (useRetryAcknowledgement && successful)
            {
                Console.WriteLine("Sending acknowledgement...");
                await ConfirmSuccessfulRetry(context, id!, acknowledgementQueue!, stoppingToken);
            }

            return successful;
        }
        catch (Exception ex)
        {
            logger.LogError($"[Middleware ERROR] {ex}");
            throw;
        }
    }

    public async Task ConfirmSuccessfulRetry(
        HandleMessageContext context,
        string retryUniqueMessageId,
        string retryAcknowledgementQueue,
        CancellationToken token)
    {
        var headers = new Dictionary<string, string>
        {
            { "ServiceControl.Retry.Successful", DateTimeOffsetHelper.ToWireFormattedString(DateTimeOffset.UtcNow) },
            { RetryUniqueMessageIdHeaderKey, retryUniqueMessageId },
            { Headers.ControlMessageHeader, bool.TrueString }
        };

        var messageBody = string.Empty;

        var messageAttributes = headers.ToDictionary(
            kvp => kvp.Key,
            kvp => new Amazon.SQS.Model.MessageAttributeValue
            {
                DataType = "String",
                StringValue = kvp.Value
            });

        var request = new SendMessageRequest
        {
            QueueUrl = retryAcknowledgementQueue,
            MessageBody = "{}", // string.Empty will throw an error with AWS
            MessageAttributes = messageAttributes
        };

        await sqs.SendMessageAsync(request, token).ConfigureAwait(false);
        //await publisher.PublishAsync(controlMessage, token).ConfigureAwait(false);
    }

    static bool IsRetriedMessage(HandleMessageContext context, out string? retryUniqueMessageId, out string? retryAcknowledgementQueue)
    {
        // check if the message is coming from a manual retry attempt
        var uniqueMessageId = context.MessageAttributes.Get(RetryUniqueMessageIdHeaderKey);
        var acknowledgementQueue = context.MessageAttributes.Get(RetryConfirmationQueueHeaderKey);

        if (uniqueMessageId is not null && acknowledgementQueue is not null)
        {
            retryUniqueMessageId = uniqueMessageId.StringValue;
            retryAcknowledgementQueue = acknowledgementQueue.StringValue;
            return true;
        }

        retryUniqueMessageId = null;
        retryAcknowledgementQueue = null;
        return false;
    }
}