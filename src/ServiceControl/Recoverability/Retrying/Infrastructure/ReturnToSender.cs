namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using ServiceControl.Persistence;

    class ReturnToSender(IErrorMessageDataStore errorMessageStore)
    {
        public virtual async Task HandleMessage(MessageContext message, IMessageDispatcher sender, CancellationToken cancellationToken = default)
        {
            var outgoingHeaders = new Dictionary<string, string>(message.Headers);

            if (outgoingHeaders.TryGetValue("ServiceControl.Retry.EnvelopeFormat", out string envelopeFormat))
            {
                outgoingHeaders.Remove("ServiceControl.Retry.EnvelopeFormat");
            }

            outgoingHeaders.Remove("ServiceControl.Retry.StagingId");
            //outgoingHeaders["ServiceControl.Retry.AcknowledgementQueue"] = errorQueueTransportAddress;

            byte[] body = null;
            var messageId = message.NativeMessageId;
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("{0}: Retrieving message body", messageId);
            }

            if (outgoingHeaders.TryGetValue("ServiceControl.Retry.Attempt.MessageId", out var attemptMessageId))
            {
                body = await FetchFromFailedMessage(outgoingHeaders, messageId, attemptMessageId);
                outgoingHeaders.Remove("ServiceControl.Retry.Attempt.MessageId");
            }
            else
            {
                Log.WarnFormat("{0}: Can't find message body. Missing header ServiceControl.Retry.Attempt.MessageId", messageId);
            }

            var outgoingMessage = new OutgoingMessage(messageId, outgoingHeaders, body ?? EmptyBody);

            var destination = outgoingHeaders["ServiceControl.TargetEndpointAddress"];
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("{0}: Forwarding message to {1}", messageId, destination);
            }

            if (!outgoingHeaders.TryGetValue("ServiceControl.RetryTo", out var retryTo))
            {
                retryTo = destination;
                outgoingHeaders.Remove("ServiceControl.TargetEndpointAddress");
            }
            else
            {
                if (Log.IsDebugEnabled)
                {
                    Log.DebugFormat("{0}: Found ServiceControl.RetryTo header. Rerouting to {1}", messageId, retryTo);
                }
            }

            var transportOp = new TransportOperation(outgoingMessage, new UnicastAddressTag(retryTo));
            transportOp.Properties.Add("EnvelopeFormat", envelopeFormat);

            await sender.Dispatch(new TransportOperations(transportOp), message.TransportTransaction, cancellationToken);

            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("{0}: Forwarded message to {1}", messageId, retryTo);
            }
        }

        async Task<byte[]> FetchFromFailedMessage(Dictionary<string, string> outgoingHeaders, string messageId, string attemptMessageId)
        {
            var uniqueMessageId = outgoingHeaders["ServiceControl.Retry.UniqueMessageId"];
            byte[] body = await errorMessageStore.FetchFromFailedMessage(uniqueMessageId);

            if (body == null)
            {
                Log.WarnFormat("{0}: Message Body not found in failed message with unique id {1} for attempt Id {1}", messageId, uniqueMessageId, attemptMessageId);
            }
            else if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("{0}: Body size: {1} bytes retrieved from failed message attachment", messageId, body.LongLength);
            }

            return body;
        }

        static readonly byte[] EmptyBody = Array.Empty<byte>();
        static readonly ILog Log = LogManager.GetLogger(typeof(ReturnToSender));
    }
}
