namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using MessageFailures;
    using NServiceBus.Logging;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using Operations.BodyStorage;
    using Raven.Client;
    using Raven.Json.Linq;

    class ReturnToSender
    {
        public ReturnToSender(IBodyStorage bodyStorage, IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
            this.bodyStorage = bodyStorage;
        }

        public virtual async Task HandleMessage(MessageContext message, IDispatchMessages sender)
        {
            byte[] body = new byte[0];
            var outgoingHeaders = new Dictionary<string, string>(message.Headers);

            outgoingHeaders.Remove("ServiceControl.Retry.StagingId");

            var messageId = message.MessageId;
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("{0}: Retrieving message body", messageId);
            }

            if (outgoingHeaders.TryGetValue("ServiceControl.Retry.Attempt.MessageId", out var attemptMessageId))
            {
                if (outgoingHeaders.Remove("ServiceControl.Retry.HasAttachment"))
                {
                    var result = await bodyStorage.TryFetch(attemptMessageId)
                        .ConfigureAwait(false);
                    if (result.HasResult)
                    {
                        using (result.Stream)
                        {
                            body = ReadFully(result.Stream);
                        }

                        if (Log.IsDebugEnabled)
                        {
                            Log.DebugFormat("{0}: Body size: {1} bytes retrieved from attachment store", messageId, body.LongLength);
                        }
                    }
                    else
                    {
                        Log.WarnFormat("{0}: Message Body not found in attachment store for attempt Id {1}", messageId, attemptMessageId);
                    }
                }
                else
                {
                    string documentId = FailedMessage.MakeDocumentId(outgoingHeaders["ServiceControl.Retry.UniqueMessageId"]);
                    var results = await documentStore.AsyncDatabaseCommands.GetAsync(new[] { documentId }, null,
                        transformer: MessagesBodyTransformer.Name).ConfigureAwait(false);
                    var loadResult = results.Results.SingleOrDefault();

                    if (loadResult != null)
                    {
                        string resultBody = ((loadResult["$values"] as RavenJArray)?.SingleOrDefault() as RavenJObject)?.ToObject<MessagesBodyTransformer.Result>()?.Body;
                        if (resultBody != null)
                        {
                            body = Encoding.UTF8.GetBytes(resultBody);

                            if (Log.IsDebugEnabled)
                            {
                                Log.DebugFormat("{0}: Body size: {1} bytes retrieved from index", messageId, body.LongLength);
                            }
                        }
                    }
                    else
                    {
                        Log.WarnFormat("{0}: Message Body not found on index for attempt Id {1}", messageId, attemptMessageId);
                    }
                }

                outgoingHeaders.Remove("ServiceControl.Retry.Attempt.MessageId");
            }
            else
            {
                Log.WarnFormat("{0}: Can't find message body. Missing header ServiceControl.Retry.Attempt.MessageId", messageId);
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

        readonly IBodyStorage bodyStorage;
        static ILog Log = LogManager.GetLogger(typeof(ReturnToSender));
        readonly IDocumentStore documentStore;
    }
}