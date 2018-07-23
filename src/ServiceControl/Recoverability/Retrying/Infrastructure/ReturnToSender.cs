namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using Operations.BodyStorage;

    public class ReturnToSender
    {
        public ReturnToSender(IBodyStorage bodyStorage)
        {
            this.bodyStorage = bodyStorage;
        }

        public virtual async Task HandleMessage(MessageContext message, IDispatchMessages sender)
        {
            var body = new byte[0];
            var outgoingHeaders = new Dictionary<string, string>(message.Headers);

            outgoingHeaders.Remove("ServiceControl.Retry.StagingId");

            var messageId = message.MessageId;
            Log.DebugFormat("{0}: Retrieving message body", messageId);

            if (outgoingHeaders.TryGetValue("ServiceControl.Retry.Attempt.MessageId", out var attemptMessageId))
            {
                var result = await bodyStorage.TryFetch(attemptMessageId)
                    .ConfigureAwait(false);
                if (result.HasResult)
                {
                    using (result.Stream)
                    {
                        body = ReadFully(result.Stream);
                    }

                    Log.DebugFormat("{0}: Body size: {1} bytes", messageId, body.LongLength);
                }
                else
                {
                    Log.WarnFormat("{0}: Message Body not found for attempt Id {1}", messageId, attemptMessageId);
                }

                outgoingHeaders.Remove("ServiceControl.Retry.Attempt.MessageId");
            }
            else
            {
                Log.WarnFormat("{0}: Can't find message body. Missing header ServiceControl.Retry.Attempt.MessageId", messageId);
            }

            var outgoingMessage = new OutgoingMessage(messageId, outgoingHeaders, body);

            var destination = outgoingHeaders["ServiceControl.TargetEndpointAddress"];
            Log.DebugFormat("{0}: Forwarding message to {1}", messageId, destination);
            if (!outgoingHeaders.TryGetValue("ServiceControl.RetryTo", out var retryTo))
            {
                retryTo = destination;
                outgoingHeaders.Remove("ServiceControl.TargetEndpointAddress");
            }
            else
            {
                Log.DebugFormat("{0}: Found ServiceControl.RetryTo header. Rerouting to {1}", messageId, retryTo);
            }

            var transportOp = new TransportOperation(outgoingMessage, new UnicastAddressTag(retryTo));

            await sender.Dispatch(new TransportOperations(transportOp), message.TransportTransaction, message.Extensions)
                .ConfigureAwait(false);
            Log.DebugFormat("{0}: Forwarded message to {1}", messageId, retryTo);
        }

        static byte[] ReadFully(Stream input)
        {
            var buffer = new byte[16 * 1024];
            using (var ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }

                return ms.ToArray();
            }
        }

        private readonly IBodyStorage bodyStorage;
        static ILog Log = LogManager.GetLogger(typeof(ReturnToSender));
    }
}