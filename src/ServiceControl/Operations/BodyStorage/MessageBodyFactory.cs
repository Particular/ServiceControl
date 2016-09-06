namespace ServiceControl.Operations.BodyStorage
{
    using System.Collections.Generic;
    using NServiceBus;

    class MessageBodyFactory
    {
        const string DefaultContentType = "text/xml";

        public MessageBodyMetadata Create(TransportMessage message)
        {
            var messageId = message.Id;
            var contentType = GetContentType(message.Headers);
            var bodySize = message.Body?.Length ?? 0;

            return new MessageBodyMetadata(messageId, contentType, bodySize);
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