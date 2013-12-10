using System;
using System.Collections.Generic;
using System.Text;

namespace ServiceControl.Operations
{
    using System.Xml;
    using MessageAuditing;
    using Newtonsoft.Json;
    using NServiceBus.Logging;

    class MessageBodyStorage
    {

        public string ContentType { get; set; }

        string DetermineContentType(IDictionary<string, string> headers)
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






        static readonly ILog Logger = LogManager.GetLogger(typeof(ProcessedMessage));
    }
}
