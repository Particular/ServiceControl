namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using MessageFailures;
    using NServiceBus.Logging;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using Raven.Client.Documents;

    class ReturnToSender
    {
        public ReturnToSender(IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
        }

        public virtual async Task HandleMessage(MessageContext message, IDispatchMessages sender)
        {
            var body = new byte[0];
            var outgoingHeaders = new Dictionary<string, string>(message.Headers);

            outgoingHeaders.Remove("ServiceControl.Retry.StagingId");
            outgoingHeaders.Remove("ServiceControl.Retry.Attempt.MessageId");

            var messageId = message.MessageId;
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("{0}: Retrieving message body", messageId);
            }

            if (outgoingHeaders.TryGetValue("ServiceControl.Retry.BodyId", out var bodyId))
            {
                using (var session = documentStore.OpenAsyncSession())
                {
                    var attachment = await session.Advanced.Attachments.GetAsync(FailedMessage.MakeDocumentId(bodyId),
                        "body").ConfigureAwait(false);

                    if (attachment == null)
                    {
                        Log.WarnFormat("{0}: Can't find message body. Missing attachment", messageId);
                    }
                    else
                    {
                        body = ReadFully(attachment.Stream);
                        if (Log.IsDebugEnabled)
                        {
                            Log.DebugFormat("{0}: Body size: {1} bytes", messageId, attachment.Details.Size);
                        }
                    }
                }
                outgoingHeaders.Remove("ServiceControl.Retry.BodyId");
            }
            else
            {
                Log.WarnFormat("{0}: Can't find message body. Missing header ServiceControl.Retry.BodyId", messageId);
            }

            var outgoingMessage = new OutgoingMessage(messageId, outgoingHeaders, body);

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

            await sender.Dispatch(new TransportOperations(transportOp), message.TransportTransaction, message.Extensions)
                .ConfigureAwait(false);

            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("{0}: Forwarded message to {1}", messageId, retryTo);
            }
        }

        // Unfortunately we can't use the buffer manager here yet because core doesn't allow to set the length property so usage of GetBuffer is not possible
        // furthermore call ToArray would neglect many of the benefits of the recyclable stream
        // https://github.com/microsoft/Microsoft.IO.RecyclableMemoryStream#getbuffer-and-toarray
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

        IDocumentStore documentStore;
        static ILog Log = LogManager.GetLogger(typeof(ReturnToSender));
    }
}