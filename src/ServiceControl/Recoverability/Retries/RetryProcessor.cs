namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.DeliveryConstraints;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Routing;
    using NServiceBus.Transports;
    using Raven.Client;
    using ServiceControl.MessageFailures;
    using ServiceControl.Operations.BodyStorage;

    class RetryProcessor
    {
        static readonly List<string> KeysToRemoveWhenRetryingAMessage = new List<string>
        {
            Headers.Retries,
            "NServiceBus.FailedQ",
            "NServiceBus.TimeOfFailure",
            "NServiceBus.ExceptionInfo.ExceptionType",
            "NServiceBus.ExceptionInfo.AuditMessage",
            "NServiceBus.ExceptionInfo.Source",
            "NServiceBus.ExceptionInfo.StackTrace"
        };

        static ILog Log = LogManager.GetLogger(typeof(RetryProcessor));

        public RetryProcessor(IBodyStorage bodyStorage, IDispatchMessages sender, IBusSession busSession, ReturnToSenderDequeuer returnToSender)
        {
            this.bodyStorage = bodyStorage;
            this.sender = sender;
            this.busSession = busSession;
            this.returnToSender = returnToSender;
        }

        public bool ProcessBatches(IDocumentSession session)
        {
            var nowForwarding = session.Include<RetryBatchNowForwarding, RetryBatch>(r => r.RetryBatchId)
                .Load<RetryBatchNowForwarding>(RetryBatchNowForwarding.Id);

            if (nowForwarding != null)
            {
                var forwardingBatch = session.Load<RetryBatch>(nowForwarding.RetryBatchId);

                if (forwardingBatch != null)
                {
                    Forward(forwardingBatch, session);
                }

                session.Delete(nowForwarding);
                return true;
            }

            isRecoveringFromPrematureShutdown = false;

            var stagingBatch = session.Query<RetryBatch>()
                .Customize(q => q.Include<RetryBatch, FailedMessageRetry>(b => b.FailureRetries))
                .FirstOrDefault(b => b.Status == RetryBatchStatus.Staging);

            if (stagingBatch != null)
            {
                if (Stage(stagingBatch, session).GetAwaiter().GetResult())
                {
                    session.Store(new RetryBatchNowForwarding { RetryBatchId = stagingBatch.Id }, RetryBatchNowForwarding.Id);
                }
                return true;
            }

            return false;
        }

        void Forward(RetryBatch forwardingBatch, IDocumentSession session)
        {
            var messageCount = forwardingBatch.FailureRetries.Count;

            if (isRecoveringFromPrematureShutdown)
            {
                returnToSender.Run(IsPartOfStagedBatch(forwardingBatch.StagingId));
            }
            else if (messageCount > 0)
            {
                returnToSender.Run(IsPartOfStagedBatch(forwardingBatch.StagingId), messageCount);
            }

            session.Delete(forwardingBatch);

            Log.InfoFormat("Retry batch {0} done", forwardingBatch.Id);
        }

        static Predicate<TransportMessage> IsPartOfStagedBatch(string stagingId)
        {
            return m =>
            {
                var messageStagingId = m.Headers["ServiceControl.Retry.StagingId"];
                return messageStagingId == stagingId;
            };
        }

        async Task<bool> Stage(RetryBatch stagingBatch, IDocumentSession session)
        {
            var stagingId = Guid.NewGuid().ToString();

            var matchingFailures = session.Load<FailedMessageRetry>(stagingBatch.FailureRetries)
                .Where(r => r != null && r.RetryBatchId == stagingBatch.Id)
                .ToArray();

            var messageIds = matchingFailures.Select(x => x.FailedMessageId).ToArray();

            if (!messageIds.Any())
            {
                Log.InfoFormat("Retry batch {0} cancelled as all matching unresolved messages are already marked for retry as part of another batch", stagingBatch.Id);
                session.Delete(stagingBatch);
                return false;
            }

            var messages = session.Load<FailedMessage>(messageIds)
                .Where(m => m != null)
                .ToArray();

            foreach (var message in messages)
            {
                await StageMessage(message, stagingId).ConfigureAwait(false);
            }

            await busSession.Publish<MessagesSubmittedForRetry>(m =>
            {
                m.FailedMessageIds = messages.Select(x => x.UniqueMessageId).ToArray();
                m.Context = stagingBatch.Context;
            }).ConfigureAwait(false);

            var msgLookup = messages.ToLookup(x => x.Id);

            stagingBatch.Status = RetryBatchStatus.Forwarding;
            stagingBatch.StagingId = stagingId;
            stagingBatch.FailureRetries = matchingFailures.Where(x => msgLookup[x.FailedMessageId].Any()).Select(x => x.Id).ToArray();

            Log.InfoFormat("Retry batch {0} staged {1} messages", stagingBatch.Id, messages.Length);
            return true;
        }

        async Task StageMessage(FailedMessage message, string stagingId)
        {
            message.Status = FailedMessageStatus.RetryIssued;

            var attempt = message.ProcessingAttempts.Last();

            var headersToRetryWith = attempt.Headers.Where(kv => !KeysToRemoveWhenRetryingAMessage.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            headersToRetryWith["ServiceControl.TargetEndpointAddress"] = attempt.FailureDetails.AddressOfFailingEndpoint;
            headersToRetryWith["ServiceControl.Retry.UniqueMessageId"] = message.UniqueMessageId;
            headersToRetryWith["ServiceControl.Retry.StagingId"] = stagingId;
            if (!string.IsNullOrWhiteSpace(attempt.ReplyToAddress))
            {
                headersToRetryWith[Headers.ReplyToAddress] = attempt.ReplyToAddress;
            }

            byte[] body = new byte[0];
            Stream stream;
            if (bodyStorage.TryFetch(attempt.MessageId, out stream))
            {
                using (stream)
                {
                    body = await ReadFully(stream).ConfigureAwait(false);
                }
            }

            var outgoingMessage = new OutgoingMessage(message.Id, headersToRetryWith, body);
            outgoingMessage.Headers[Headers.CorrelationId] = attempt.CorrelationId;
            outgoingMessage.Headers[Headers.MessageIntent] = attempt.MessageIntent.ToString();
            List<DeliveryConstraint> constraints = new List<DeliveryConstraint>();
            if (!attempt.Recoverable)
            {
                constraints.Add(new NonDurableDelivery());
            }

            var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(returnToSender.InputAddress.ToString()), deliveryConstraints: constraints);

            await sender.Dispatch(new TransportOperations(operation), new ContextBag()).ConfigureAwait(false);
        }

        static async Task<byte[]> ReadFully(Stream input)
        {
            var buffer = new byte[16 * 1024];
            using (var ms = new MemoryStream())
            {
                int read;
                while ((read = await input.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                {
                    await ms.WriteAsync(buffer, 0, read).ConfigureAwait(false);
                }
                return ms.ToArray();
            }
        }

        IBodyStorage bodyStorage;
        IDispatchMessages sender;
        IBusSession busSession;
        ReturnToSenderDequeuer returnToSender;
        bool isRecoveringFromPrematureShutdown = true;
    }
}