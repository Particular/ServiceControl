namespace NServiceBus.Transport.Msmq
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using MSMQ.Messaging;
    using System.Text;
    using System.Xml;
    using DeliveryConstraints;
    using Logging;
    using Performance.TimeToBeReceived;
    using Transport;

    class MsmqUtilities
    {
        static MsmqAddress GetIndependentAddressForQueue(MessageQueue q)
        {
            var arr = q.FormatName.Split('\\');
            var queueName = arr[arr.Length - 1];

            var directPrefixIndex = arr[0].IndexOf(DIRECTPREFIX, StringComparison.Ordinal);
            if (directPrefixIndex >= 0)
            {
                return new MsmqAddress(queueName, arr[0].Substring(directPrefixIndex + DIRECTPREFIX.Length));
            }

            var tcpPrefixIndex = arr[0].IndexOf(DIRECTPREFIX_TCP, StringComparison.Ordinal);
            if (tcpPrefixIndex >= 0)
            {
                return new MsmqAddress(queueName, arr[0].Substring(tcpPrefixIndex + DIRECTPREFIX_TCP.Length));
            }

            try
            {
                // the pessimistic approach failed, try the optimistic approach
                arr = q.QueueName.Split('\\');
                queueName = arr[arr.Length - 1];
                return new MsmqAddress(queueName, q.MachineName);
            }
            catch
            {
                throw new Exception($"Could not translate format name to independent name: {q.FormatName}");
            }
        }

        public static Dictionary<string, string> ExtractHeaders(Message msmqMessage)
        {
            var headers = DeserializeMessageHeaders(msmqMessage);

            //note: we can drop this line when we no longer support interop btw v3 + v4
            if (msmqMessage.ResponseQueue != null && !headers.ContainsKey(Headers.ReplyToAddress))
            {
                headers[Headers.ReplyToAddress] = GetIndependentAddressForQueue(msmqMessage.ResponseQueue).ToString();
            }

            if (Enum.IsDefined(typeof(MessageIntentEnum), msmqMessage.AppSpecific) && !headers.ContainsKey(Headers.MessageIntent))
            {
                headers[Headers.MessageIntent] = ((MessageIntentEnum)msmqMessage.AppSpecific).ToString();
            }

            headers[Headers.CorrelationId] = GetCorrelationId(msmqMessage, headers);
            return headers;
        }

        static string GetCorrelationId(Message message, Dictionary<string, string> headers)
        {
            if (headers.TryGetValue(Headers.CorrelationId, out var correlationId))
            {
                return correlationId;
            }

            if (message.CorrelationId == "00000000-0000-0000-0000-000000000000\\0")
            {
                return null;
            }

            //msmq required the id's to be in the {guid}\{incrementing number} format so we need to fake a \0 at the end that the sender added to make it compatible
            //The replace can be removed in v5 since only v3 messages will need this
            return message.CorrelationId.Replace("\\0", string.Empty);
        }

        static Dictionary<string, string> DeserializeMessageHeaders(Message m)
        {
            return DeserializeMessageHeaders(m.Extension);
        }

        internal static Dictionary<string, string> DeserializeMessageHeaders(byte[] bytes)
        {
            var result = new Dictionary<string, string>();

            if (bytes.Length == 0)
            {
                return result;
            }

            //This is to make us compatible with v3 messages that are affected by this bug:
            //http://stackoverflow.com/questions/3779690/xml-serialization-appending-the-0-backslash-0-or-null-character
            var data = bytes;
            var xmlLength = data.LastIndexOf(EndTag) + EndTag.Length; // Ignore any data after last </ArrayOfHeaderInfo>
            object o;
            using (var stream = new MemoryStream(buffer: data, index: 0, count: xmlLength, writable: false, publiclyVisible: true))
            {
                using (var reader = XmlReader.Create(stream, new XmlReaderSettings
                {
                    CheckCharacters = false
                }))
                {
                    o = headerSerializer.Deserialize(reader);
                }
            }

            foreach (var pair in (List<HeaderInfo>)o)
            {
                if (pair.Key != null)
                {
                    result.Add(pair.Key, pair.Value);
                }
            }

            return result;
        }

        public static Message Convert(OutgoingMessage message, List<DeliveryConstraint> deliveryConstraints)
        {
            var result = new Message();

            if (message.Body != null)
            {
                result.BodyStream = new MemoryStream(message.Body);
            }


            AssignMsmqNativeCorrelationId(message, result);
            result.Recoverable = !deliveryConstraints.Any(c => c is NonDurableDelivery);

            if (deliveryConstraints.TryGet(out DiscardIfNotReceivedBefore timeToBeReceived) && timeToBeReceived.MaxTime < MessageQueue.InfiniteTimeout)
            {
                result.TimeToBeReceived = timeToBeReceived.MaxTime;
            }

            var addCorrIdHeader = !message.Headers.ContainsKey("CorrId");

            using (var stream = new MemoryStream())
            {
                var headers = message.Headers.Select(pair => new HeaderInfo
                {
                    Key = pair.Key,
                    Value = pair.Value
                }).ToList();

                if (addCorrIdHeader)
                {
                    headers.Add(new HeaderInfo
                    {
                        Key = "CorrId",
                        Value = result.CorrelationId
                    });
                }

                headerSerializer.Serialize(stream, headers);
                result.Extension = stream.ToArray();
            }

            var messageIntent = default(MessageIntentEnum);

            if (message.Headers.TryGetValue(Headers.MessageIntent, out var messageIntentString))
            {
                Enum.TryParse(messageIntentString, true, out messageIntent);
            }

            result.AppSpecific = (int)messageIntent;


            return result;
        }

        static void AssignMsmqNativeCorrelationId(OutgoingMessage message, Message result)
        {
            if (!message.Headers.TryGetValue(Headers.CorrelationId, out var correlationIdHeader))
            {
                return;
            }

            if (string.IsNullOrEmpty(correlationIdHeader))
            {
                return;
            }


            if (Guid.TryParse(correlationIdHeader, out _))
            {
                //msmq required the id's to be in the {guid}\{incrementing number} format so we need to fake a \0 at the end to make it compatible
                result.CorrelationId = $"{correlationIdHeader}\\0";
                return;
            }

            try
            {
                if (correlationIdHeader.Contains("\\"))
                {
                    var parts = correlationIdHeader.Split('\\');


                    if (parts.Length == 2 && Guid.TryParse(parts.First(), out _) &&
                        int.TryParse(parts[1], out _))
                    {
                        result.CorrelationId = correlationIdHeader;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to assign a native correlation id for message: {message.MessageId}", ex);
            }
        }

        public const string PropertyHeaderPrefix = "NServiceBus.Timeouts.Properties.";
        const string DIRECTPREFIX = "DIRECT=OS:";
        const string DIRECTPREFIX_TCP = "DIRECT=TCP:";
        internal const string PRIVATE = "\\private$\\";

        static System.Xml.Serialization.XmlSerializer headerSerializer = new System.Xml.Serialization.XmlSerializer(typeof(List<HeaderInfo>));
        static ILog Logger = LogManager.GetLogger<MsmqUtilities>();
        static byte[] EndTag = Encoding.UTF8.GetBytes("</ArrayOfHeaderInfo>");
    }
}