namespace Particular.ServiceControl.Data
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using NServiceBus;

    /// <summary>
    /// Static class containing headers used by NServiceBus.
    /// </summary>
    public static class HeaderId
    {
        public const byte NotRegistered = byte.MaxValue;

        static readonly Dictionary<string, byte> NameToId;
        static readonly Dictionary<string, byte[]> NameToIdAsByteArray;
        static readonly Dictionary<byte, string> IDToName;

        static HeaderId()
        {
            NameToId = new Dictionary<string, byte>();
            byte i = 1;

            void Add(string value, ref byte id)
            {
                NameToId.Add(value, id);
                id += 1;
            }

            Add(Headers.HttpFrom, ref i);
            Add(Headers.HttpTo, ref i);
            Add(Headers.RouteTo, ref i);
            Add(Headers.DestinationSites, ref i);
            Add(Headers.OriginatingSite, ref i);
            Add(Headers.SagaId, ref i);
            Add(Headers.MessageId, ref i);
            Add(Headers.CorrelationId, ref i);
            Add(Headers.ReplyToAddress, ref i);
            Add(Headers.HeaderName, ref i);
            Add(Headers.NServiceBusVersion, ref i);
            Add(Headers.ReturnMessageErrorCodeHeader, ref i);
            Add(Headers.ControlMessageHeader, ref i);
            Add(Headers.SagaType, ref i);
            Add(Headers.OriginatingSagaId, ref i);
            Add(Headers.OriginatingSagaType, ref i);
            Add(Headers.Retries, ref i);
            Add(Headers.FLRetries, ref i);
            Add(Headers.Retries, ref i);
            Add(Headers.ProcessingStarted, ref i);
            Add(Headers.ProcessingEnded, ref i);
            Add(Headers.TimeSent, ref i);
            Add(Headers.RelatedTo, ref i);
            Add(Headers.EnclosedMessageTypes, ref i);
            Add(Headers.ContentType, ref i);
            Add(Headers.SubscriptionMessageType, ref i);
            Add("NServiceBus.SubscriberAddress", ref i);
            Add("NServiceBus.SubscriberEndpoint", ref i);
            Add(Headers.IsSagaTimeoutMessage, ref i);
            Add(Headers.IsDeferredMessage, ref i);
            Add(Headers.OriginatingEndpoint, ref i);
            Add(Headers.OriginatingMachine, ref i);
            Add(Headers.OriginatingHostId, ref i);
            Add(Headers.ProcessingEndpoint, ref i);
            Add(Headers.ProcessingMachine, ref i);
            Add(Headers.HostDisplayName, ref i);
            Add(Headers.HostId, ref i);
            Add(Headers.HasLicenseExpired, ref i);
            Add(Headers.OriginatingAddress, ref i);
            Add(Headers.ConversationId, ref i);
            Add(Headers.MessageIntent, ref i);
            Add("NServiceBus.TimeToBeReceived", ref i);
            Add("NServiceBus.NonDurableMessage", ref i);

            NameToIdAsByteArray = NameToId.ToDictionary(kvp => kvp.Key, kvp => new[] { kvp.Value });
            IDToName = NameToId.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
        }

        public static bool TryGetId(string headerName, out byte id) => NameToId.TryGetValue(headerName, out id);
        public static string GetName(byte id) => IDToName[id];

        static readonly UTF8Encoding NoBom = new UTF8Encoding(false);

        public static byte[] EncodeName(string name)
        {
            if (NameToIdAsByteArray.TryGetValue(name, out var id))
            {
                return id;
            }

            using (var ms = new MemoryStream())
            {
                using (var writer = new BinaryWriter(ms, NoBom, true))
                {
                    writer.Write(NotRegistered);
                    writer.Write(name);
                }

                return ms.ToArray();
            }
        }

        public static string DecodeName(byte[] name)
        {
            if (name.Length == 1)
            {
                return IDToName[name[0]];
            }

            using (var ms = new MemoryStream(name))
            {
                using (var reader = new BinaryReader(ms, NoBom))
                {
                    reader.ReadByte(); // NotRegistered
                    return reader.ReadString();
                }
            }
        }
    }
}