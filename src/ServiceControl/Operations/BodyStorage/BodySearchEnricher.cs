namespace ServiceControl.Operations.MessageStorage
{
    using System.Text;
    using Contracts.Operations;
    using NServiceBus;
    using NServiceBus.Logging;

    public class BodySearchEnricher : ImportEnricher
    {
        public override void Enrich(ImportMessage message)
        {
            var contentLength = 0;

            if (message.PhysicalMessage.Body != null)
            {
                contentLength = message.PhysicalMessage.Body.Length;
            }

            string contentType;

            if (!message.PhysicalMessage.Headers.TryGetValue(Headers.ContentType, out contentType))
            {
                contentType = "application/xml"; //default to xml for now
            }


            message.Add(new MessageMetadata("ContentLength", contentLength));
            message.Add(new MessageMetadata("ContentType", contentType));

            if (contentLength == 0)
            {
                return;
            }

            if (contentType.ToLower().Contains("binary"))
            {
                return;
            }

            if (message.PhysicalMessage.Body.Length > MaxBodySizeToMakeSearchable)
            {
                Logger.InfoFormat("Message '{0}' has a content length of {1} which is above the threshold, message body won't be searchable", message.UniqueMessageId, contentLength, MaxBodySizeToMakeSearchable);
                return;
            }
            message.Add(new MessageMetadata("MessageBodySearchString", null,new []
            {
                Encoding.UTF8.GetString(message.PhysicalMessage.Body)
            }));

        }

        static int MaxBodySizeToMakeSearchable = 1024 * 500; //500 kb
        static ILog Logger = LogManager.GetLogger(typeof(BodySearchEnricher));
    }
}