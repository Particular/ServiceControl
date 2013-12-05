namespace ServiceControl
{
    using System;
    using NServiceBus;

    public static class TransportMessageExtentions
    {
        public static string ProcessingEndpoint(this TransportMessage message)
        {
            string endpoint;

            if (message.Headers.TryGetValue(Headers.ProcessingEndpoint, out endpoint))
            {
                return endpoint;
            }

            if (message.ReplyToAddress != null)
            {
                return message.ReplyToAddress.Queue;
            }

            throw new InvalidOperationException("No processing endpoint could be determined for message");
        }

        public static string UniqueId(this TransportMessage message)
        {
            return string.Format("{0}-{1}", message.Id.Replace(@"\", "-"), message.ProcessingEndpoint());
        }

    }
}