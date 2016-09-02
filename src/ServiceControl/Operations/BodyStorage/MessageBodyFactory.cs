namespace ServiceControl.Operations.BodyStorage
{
    using System.Collections.Generic;
    using NServiceBus;

    class MessageBodyFactory
    {
        const string DefaultContentType = "text/xml";

        public IMessageBody Create(TransportMessage message)
        {
            var messageId = message.Id;
            var contentType = GetContentType(message.Headers);
            var bodySize = message.Body?.Length ?? 0;

            var metadata = new MessageBodyMetadata(messageId, contentType, bodySize);

            return new ByteArrayMessageBody(metadata, message.Body);

        }

        private static string GetContentType(Dictionary<string, string> headers, string defaultContentType = DefaultContentType)
        {
            string contentType;

            if (!headers.TryGetValue(Headers.ContentType, out contentType))
            {
                contentType = defaultContentType;
            }

            return contentType;
        }
    }
}