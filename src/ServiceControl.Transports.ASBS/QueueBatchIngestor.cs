namespace ServiceControl.Transports.ASBS
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Core;
    using Microsoft.Azure.ServiceBus.InteropExtensions;
    using NServiceBus;
    using NServiceBus.Logging;

    public class QueueBatchIngestor : IConsumeBatches
    {
        public void Start(Func<IReadOnlyCollection<BatchMessage>, Task> consume, string inputQueue, string connectionString)
        {
            tokenSource = new CancellationTokenSource();
            receiver = new MessageReceiver(connectionString, inputQueue, ReceiveMode.PeekLock, RetryPolicy.Default, 100);
            consumeTask = Consume(consume, receiver, tokenSource.Token);
        }

        public async Task Stop()
        {
            tokenSource.Cancel();
            await consumeTask.ConfigureAwait(false);
            await receiver.CloseAsync().ConfigureAwait(false);
        }

        static async Task Consume(Func<IReadOnlyCollection<BatchMessage>, Task> consume, MessageReceiver receiver, CancellationToken cancellationToken)
        {
            var batchMessages = new List<BatchMessage>(10);
            var lockTokens = new List<string>(10);
            var stopwatch = new Stopwatch();
            while (!cancellationToken.IsCancellationRequested)
            {
                batchMessages.Clear();
                lockTokens.Clear();
                stopwatch.Reset();

                var messages = await receiver.ReceiveAsync(10, TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                if (messages == null)
                {
                    continue;
                }

                stopwatch.Start();
                Log.Warn("Start ingestion of messages.");
                foreach (var message in messages)
                {
                    lockTokens.Add(message.SystemProperties.LockToken);
                    batchMessages.Add(new BatchMessage(message.GetMessageId(), message.GetNServiceBusHeaders(), message.GetBody()));
                }

                await consume(batchMessages).ConfigureAwait(false);

                await receiver.CompleteAsync(lockTokens).ConfigureAwait(false);

                stopwatch.Stop();
                Log.Warn($"Ingestion Took {stopwatch.ElapsedMilliseconds} ms");
            }
        }

        MessageReceiver receiver;
        CancellationTokenSource tokenSource;
        Task consumeTask;
        static ILog Log = LogManager.GetLogger<QueueBatchIngestor>();
    }

    static class MessageExtensions
    {
        public static Dictionary<string, string> GetNServiceBusHeaders(this Message message)
        {
            var headers = new Dictionary<string, string>(message.UserProperties.Count);

            foreach (var kvp in message.UserProperties)
            {
                headers[kvp.Key] = kvp.Value?.ToString();
            }

            headers.Remove("NServiceBus.Transport.Encoding");

            if (!string.IsNullOrWhiteSpace(message.ReplyTo))
            {
                headers[Headers.ReplyToAddress] = message.ReplyTo;
            }

            if (!string.IsNullOrWhiteSpace(message.CorrelationId))
            {
                headers[Headers.CorrelationId] = message.CorrelationId;
            }

            return headers;
        }

        public static string GetMessageId(this Message message)
        {
            if (string.IsNullOrEmpty(message.MessageId))
            {
                throw new Exception("Azure Service Bus MessageId is required, but was not found. Ensure to assign MessageId to all Service Bus messages.");
            }

            return message.MessageId;
        }

        public static byte[] GetBody(this Message message)
        {
            if (message.UserProperties.TryGetValue("NServiceBus.Transport.Encoding", out var value) && value.Equals("wcf/byte-array"))
            {
                return message.GetBody<byte[]>() ?? Array.Empty<byte>();
            }

            return message.Body ?? Array.Empty<byte>();
        }
    }
}