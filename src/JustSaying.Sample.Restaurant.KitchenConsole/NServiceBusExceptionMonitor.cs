using System;
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
            // var x = JsonSerializer.Deserialize<JustSaying.Models.Message>(message.Body);
            var x = JsonNode.Parse(message.Body);
            var exception = JsonSerializer.Serialize(new
            {
                ex.Message,
                message.MessageId,
                ex.StackTrace,
                type = x["Subject"].ToString().ToLower()
            });
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
            Console.WriteLine($"NServiceBusHandler.Handled: {message.GetType().Name}");
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