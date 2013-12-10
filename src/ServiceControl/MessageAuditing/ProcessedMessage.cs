namespace ServiceControl.MessageAuditing
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Xml;
    using NServiceBus;
    using NServiceBus.Logging;
    using Contracts.Operations;
    using JsonConvert = Newtonsoft.Json.JsonConvert;

    public class ProcessedMessage
    {
        public ProcessedMessage()
        {
        }

        public ProcessedMessage(ImportSuccessfullyProcessedMessage message)
        {

            Id = message.UniqueMessageId;
            
            ReceivingEndpoint = message.ReceivingEndpoint;

            SendingEndpoint = message.SendingEndpoint;

            MessageProperties = message.Properties;

            string processedAt;

            if (message.PhysicalMessage.Headers.TryGetValue(Headers.ProcessingEnded, out processedAt))
            {
                ProcessedAt = DateTimeExtensions.ToUtcDateTime(processedAt);
            }
            else
            {
                ProcessedAt = DateTime.UtcNow;//best guess    
            }
        }

        public Dictionary<string, MessageProperty> MessageProperties { get; set; }

        public string Id { get; set; }
        public PhysicalMessage PhysicalMessage { get; set; }

        public DateTime ProcessedAt { get; set; }

        public string Body { get; set; }

        public EndpointDetails SendingEndpoint { get; set; }

        public EndpointDetails ReceivingEndpoint { get; set; }


        public string ContentType { get; set; }

        string DetermineContentType(IDictionary<string,string> headers)
        {
            var contentType = "application/xml"; //default to xml for now
            DictionaryExtensions.CheckIfKeyExists(NServiceBus.Headers.ContentType, headers, s => contentType = s);
            return contentType;
        }

        static string DeserializeBody(byte[] body, string contentType)
        {
            var bodyString = Encoding.UTF8.GetString(body);

            if (contentType == "application/json" || contentType == "text/json")
            {
                return bodyString;
            }

            if (contentType != "text/xml" && contentType != "application/xml")
            {
                return null;
            }

            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(bodyString);
                return JsonConvert.SerializeXmlNode(doc.DocumentElement);
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to convert XML payload to json", ex);
                return null;
            }
        }

       

      
        MessageStatistics GetProcessingStatistics(IDictionary<string,string> headers)
        {
            var messageStatistics = new MessageStatistics();
            var processingEnded = DateTime.MinValue;
            var timeSent = DateTime.MinValue;
            var processingStarted = DateTime.MinValue;

            DictionaryExtensions.CheckIfKeyExists(NServiceBus.Headers.ProcessingEnded, headers, (ended) =>
            {
                processingEnded = DateTimeExtensions.ToUtcDateTime(ended);
            });
            DictionaryExtensions.CheckIfKeyExists(NServiceBus.Headers.TimeSent, headers, (time) =>
            {
                timeSent = DateTimeExtensions.ToUtcDateTime(time);
            });
            DictionaryExtensions.CheckIfKeyExists(NServiceBus.Headers.ProcessingStarted, headers, (started) =>
            {
                processingStarted = DateTimeExtensions.ToUtcDateTime(started);
            });

            if (processingEnded != DateTime.MinValue && timeSent != DateTime.MinValue)
            {
                messageStatistics.CriticalTime = processingEnded - timeSent;
            }
            if (processingEnded != DateTime.MinValue && processingStarted != DateTime.MinValue)
            {
                messageStatistics.ProcessingTime = processingEnded - processingStarted;
            }

            return messageStatistics;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(ProcessedMessage));
    }
}