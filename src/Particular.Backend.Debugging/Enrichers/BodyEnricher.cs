namespace Particular.Backend.Debugging.Enrichers
{
    using NServiceBus;
    using Particular.Operations.Ingestion.Api;

    public class BodyEnricher : IEnrichAuditMessageSnapshots
    {
        public void Enrich(IngestedMessage message, AuditMessageSnapshot snapshot)
        {
            const int MaxBodySizeToStore = 1024 * 100; //100 kb


            if (!message.HasBody)
            {
                return;
            }

            string contentType;

            if (!message.Headers.TryGet(Headers.ContentType, out contentType))
            {
                contentType = "text/xml"; //default to xml for now
            }
           
            var bodyId = message.Id;
            var bodyUrl = string.Format("/messages/{0}/body", bodyId);

            
            string bodyText = null;
            if (!contentType.Contains("binary") && message.BodyLength <= MaxBodySizeToStore)
            {
                bodyText = System.Text.Encoding.UTF8.GetString(message.Body);
            }
            snapshot.Body = new BodyInformation
            {
                ContentType = contentType,
                ContentLenght = message.BodyLength,
                BodyUrl = bodyUrl,
                Text = bodyText
            };
        }
    }
}