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
            var outgoingHeaders = new Dictionary<string, string>(message.Headers);

            outgoingHeaders.Remove("ServiceControl.Retry.StagingId");

            byte[] body = null;
            var messageId = message.MessageId;
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("{0}: Retrieving message body", messageId);
            }

            if (outgoingHeaders.TryGetValue("ServiceControl.Retry.Attempt.MessageId", out var attemptMessageId))
            {
                if (outgoingHeaders.Remove("ServiceControl.Retry.BodyOnFailedMessage"))
                {
                    body = await FetchFromFailedMessage(outgoingHeaders, messageId, attemptMessageId)
                        .ConfigureAwait(false);
                }
                else
                {
                    body = await FetchFromBodyStore(attemptMessageId, messageId)
                        .ConfigureAwait(false);
                }

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

            await sender.Dispatch(new TransportOperations(transportOp), message.TransportTransaction, message.Extensions)
                .ConfigureAwait(false);

            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("{0}: Forwarded message to {1}", messageId, retryTo);
            }
        }

        async Task<byte[]> FetchFromFailedMessage(Dictionary<string, string> outgoingHeaders, string messageId, string attemptMessageId)
        {
            byte[] body = null;
            string documentId = FailedMessage.MakeDocumentId(outgoingHeaders["ServiceControl.Retry.UniqueMessageId"]);
            var results = await documentStore.AsyncDatabaseCommands.GetAsync(new[] { documentId }, null,
                transformer: MessagesBodyTransformer.Name).ConfigureAwait(false);

            string resultBody = ((results.Results?.SingleOrDefault()?["$values"] as RavenJArray)?.SingleOrDefault() as RavenJObject)
                ?.ToObject<MessagesBodyTransformer.Result>()?.Body;

            if (resultBody != null)
            {
                body = Encoding.UTF8.GetBytes(resultBody);

                if (Log.IsDebugEnabled)
                {
                    Log.DebugFormat("{0}: Body size: {1} bytes retrieved from index", messageId, body.LongLength);
                }
            }
            else
            {
                Log.WarnFormat("{0}: Message Body not found on index for attempt Id {1}", messageId, attemptMessageId);
            }
            return body;
        }

        async Task<byte[]> FetchFromBodyStore(string attemptMessageId, string messageId)
        {
            byte[] body = null;
            var result = await bodyStorage.TryFetch(attemptMessageId)
                .ConfigureAwait(false);
            if (result.HasResult)
            {
                using (result.Stream)
                {
                    // Unfortunately we can't use the buffer manager here yet because core doesn't allow to set the length property so usage of GetBuffer is not possible
                    // furthermore call ToArray would neglect many of the benefits of the recyclable stream
                    // RavenDB always returns a memory stream so there is no need to pretend we need to do buffered reads since the memory is anyway fully allocated already
                    // this assumption might change when the database is upgraded but right now this is the most memory efficient way to do things
                    // https://github.com/microsoft/Microsoft.IO.RecyclableMemoryStream#getbuffer-and-toarray
                    body = ((MemoryStream)result.Stream).ToArray();
                }

                if (Log.IsDebugEnabled)
                {
                    Log.DebugFormat("{0}: Body size: {1} bytes retrieved from attachment store", messageId, body.LongLength);
                }
            }
            else
            {
                Log.WarnFormat("{0}: Message Body not found in attachment store for attempt Id {1}", messageId,
                    attemptMessageId);
            }
            return body;
        }

        static readonly byte[] EmptyBody = new byte[0];
        readonly IBodyStorage bodyStorage;
        static ILog Log = LogManager.GetLogger(typeof(ReturnToSender));
        readonly IDocumentStore documentStore;
    }
}
