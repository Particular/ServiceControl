namespace ServiceControl.Operations.BodyStorage
{
    using System.IO;
    using Contracts.Operations;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Settings;

    public class BodyStorageEnricher : ImportEnricher
    {
        public IBodyStorage BodyStorage { get; set; }

        public override void Enrich(ImportMessage message)
        {
            if (message.PhysicalMessage.Body == null || message.PhysicalMessage.Body.Length == 0)
            {
                message.Metadata.Add("ContentLength", 0);
                return;
            }

            string contentType;

            if (!message.PhysicalMessage.Headers.TryGetValue(Headers.ContentType, out contentType))
            {
                contentType = "text/xml"; //default to xml for now
            }

            message.Metadata.Add("ContentType", contentType);

            var bodySize = message.PhysicalMessage.Body.Length;

            var bodyId = message.MessageId;

            if (message is ImportFailedMessage || bodySize <= Settings.MaxBodySizeToStore)
            {
                StoreBody(message, bodyId, contentType, bodySize);  
            }
            else
            {
                var bodyUrl = string.Format("/messages/{0}/body", bodyId);
                message.Metadata.Add("BodyUrl", bodyUrl);
                message.Metadata.Add("BodyNotStored", true);
            }

            // Issue #296 Body Storage Enricher config
            if (!contentType.Contains("binary") && bodySize <= Settings.MaxBodySizeToStore)
            {
                message.Metadata.Add("Body", System.Text.Encoding.UTF8.GetString(message.PhysicalMessage.Body));
            }
          
            message.Metadata.Add("ContentLength", bodySize);
        }

        void StoreBody(ImportMessage message, string bodyId, string contentType, int bodySize)
        {
            using (var bodyStream = new MemoryStream(message.PhysicalMessage.Body))
            {
                var bodyUrl = BodyStorage.Store(bodyId, contentType, bodySize, bodyStream);
                message.Metadata.Add("BodyUrl", bodyUrl);
            }
        }
    }
}