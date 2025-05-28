using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using JustSaying.Messaging.Middleware;
using JustSaying.Messaging.Monitoring;
using Microsoft.Extensions.Hosting.Internal;

namespace JustSaying.Sample.Restaurant.KitchenConsole
{
    internal class NServiceBusExceptionMonitor(IAmazonSQS sqsClient) : IMessageMonitor
    {
        public void HandleException(Type messageType)
        {
            // Console.WriteLine($"Exception: {messageType.FullName}");
        }

        public async void HandleError(Exception ex, Message message)
        {
            Console.WriteLine($"NServiceBusHandler: {ex.Message}");
            var url = await sqsClient.GetQueueUrlAsync("JustSaying_exceptions");
            var nserviceBusHeaders = new Dictionary<string, string>();
            nserviceBusHeaders["NserviceBus.MessageId"] = message.MessageId;
            nserviceBusHeaders["NServiceBus.ExceptionInfo.ExceptionType"] = ex.GetType().FullName;
            nserviceBusHeaders["NServiceBus.ExceptionInfo.StackTrace"] = ex.ToString();
            var exception = JsonSerializer.Serialize(nserviceBusHeaders);
            await sqsClient.SendMessageAsync(new SendMessageRequest(url.QueueUrl,exception));
        }

        public void HandleTime(TimeSpan duration)
        {
        }

        public void IssuePublishingMessage()
        {
        }

        public void Handled(JustSaying.Models.Message message)
        {
        }

        public void IncrementThrottlingStatistic()
        {
        }

        public void HandleThrottlingTime(TimeSpan duration)
        {
        }

        public void PublishMessageTime(TimeSpan duration)
        {
        }

        public void ReceiveMessageTime(TimeSpan duration, string queueName, string region)
        {
        }

        public void HandlerExecutionTime(Type handlerType, Type messageType, TimeSpan duration)
        {
        }
    }
}