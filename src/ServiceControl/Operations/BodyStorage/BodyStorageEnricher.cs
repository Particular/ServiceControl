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
                return;
            }

            string contentType;

            if (!message.PhysicalMessage.Headers.TryGetValue(Headers.ContentType, out contentType))
            {
                contentType = "text/xml"; //default to xml for now
            }

            var bodySize = message.PhysicalMessage.Body.Length;

            var bodyId = message.UniqueMessageId;

            using (var bodyStream = new MemoryStream(message.PhysicalMessage.Body))
            {
                var bodyUrl = BodyStorage.Store(bodyId, contentType, bodySize, bodyStream);
                message.Add(new MessageMetadata("BodyUrl",bodyUrl));

            }

            
            message.Add(new MessageMetadata("BodySize", bodySize));
        }
    }
}