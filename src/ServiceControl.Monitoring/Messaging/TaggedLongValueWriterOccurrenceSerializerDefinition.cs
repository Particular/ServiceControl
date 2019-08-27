namespace ServiceControl.Monitoring.Messaging
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using NServiceBus.MessageInterfaces;
    using NServiceBus.Serialization;
    using NServiceBus.Settings;

    class TaggedLongValueWriterOccurrenceSerializerDefinition : SerializationDefinition
    {
        public override Func<IMessageMapper, IMessageSerializer> Configure(ReadOnlySettings settings)
        {
            return mapper => new TaggedLongValueSerializer();
        }
    }

    class TaggedLongValueSerializer : IMessageSerializer
    {
        static readonly object[] NoMessages = new object[0];

        public void Serialize(object message, Stream stream)
        {
            throw new NotImplementedException();
        }

        // 0                   1                   2                   3
        // 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
        //+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        //|     Version   | Min date time |tag cnt|tag1key|tag1len| tag1..|
        //+-------+-------+-------+-------+---------------+-------+-------+
        //| ..bytes       |tag2key|tag2len| tag2 bytes .................  |
        //+-------+-------+-------+-------+---------------+-------+-------+
        //| tag2 bytes continued                  | count | date1 | tag1  |
        //+-------+-------+-------+-------+---------------+-------+-------+
        //|    value 1    | date2 | tag2  |     value 2   | date3 | ...   |
        //+-------+---------------+-------+---------------+-------+-------+
        public object[] Deserialize(Stream stream, IList<Type> messageTypes = null)
        {
            var reader = new BinaryReader(stream);

            var version = reader.ReadInt64();

            if (version == 1)
            {
                var baseTicks = reader.ReadInt64();

                var tagsCount = reader.ReadInt32();

                var tagKeyToValue = DeserializeTags(tagsCount, reader);

                var count = reader.ReadInt32();

                if (count == 0)
                {
                    return NoMessages;
                }

                var tagKeyToMessage = new Dictionary<int, TaggedLongValueOccurrence>();

                foreach (var keyToValue in tagKeyToValue)
                {
                    var message = CreateMessageForTag(keyToValue.Value);

                    tagKeyToMessage.Add(keyToValue.Key, message);
                }

                var allMessages = new List<TaggedLongValueOccurrence>(tagsCount); // usual case

                for (var i = 0; i < count; i++)
                {
                    var ticks = reader.ReadInt32();
                    var timestamp = baseTicks + ticks;
                    var tagKey = reader.ReadInt32();
                    var value = reader.ReadInt64();

                    var message = tagKeyToMessage[tagKey];

                    if (message.IsFull)
                    {
                        allMessages.Add(message);

                        message = CreateMessageForTag(tagKeyToValue[tagKey]);

                        tagKeyToMessage[tagKey] = message;
                    }

                    if (message.TryRecord(timestamp, value) == false)
                    {
                        throw new Exception("The value should have been written to a newly leased message");
                    }
                }

                allMessages.AddRange(tagKeyToMessage.Values);

                return allMessages.ToArray();
            }

            throw new Exception($"The message version number '{version}' cannot be handled properly.");
        }

        static TaggedLongValueOccurrence CreateMessageForTag(string keyValue)
        {
            var message = RawMessage.Pool<TaggedLongValueOccurrence>.Default.Lease();
            message.TagValue = keyValue;

            return message;
        }

        static Dictionary<int, string> DeserializeTags(int tagCount, BinaryReader reader)
        {
            var tagKeyToValue = new Dictionary<int, string>();

            for (var i = 0; i < tagCount; i++)
            {
                var tagKey = reader.ReadInt32();
                var tagLen = reader.ReadInt32();
                var tagValue = TagDecoder.GetString(reader.ReadBytes(tagLen));

                tagKeyToValue.Add(tagKey, tagValue);
            }

            return tagKeyToValue;
        }

        public string ContentType { get; } = "TaggedLongValueWriterOccurrence";

        private static UTF8Encoding TagDecoder = new UTF8Encoding(false);
    }
}