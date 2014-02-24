namespace ServiceControl.Operations.BodyStorage
{
    using System.IO;
    using Contracts.Operations;
    using NServiceBus;

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

            if (message is ImportFailedMessage)
            {
                using (var bodyStream = new MemoryStream(message.PhysicalMessage.Body))
                {
                    var bodyUrl = BodyStorage.Store(bodyId, contentType, bodySize, bodyStream);
                    message.Metadata.Add("BodyUrl", bodyUrl);
                }                
            }
            else
            {
                var bodyUrl = string.Format("/messages/{0}/body", bodyId);
                message.Metadata.Add("BodyUrl", bodyUrl);
            }

            if (!contentType.Contains("binary") && bodySize <= MaxBodySizeToStore)
            {
                message.Metadata.Add("Body", System.Text.Encoding.UTF8.GetString(message.PhysicalMessage.Body));
            }

            message.Metadata.Add("ContentLength", bodySize);
        }

        const int MaxBodySizeToStore = 1024 * 100; //100 kb
    }
}