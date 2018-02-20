namespace Particular.ServiceControl.Data
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using ProtoBuf;

    /// <summary>
    /// This class provides a serializer for a dictionary of metadata values, that is written in a space efficient manner.
    /// </summary>
    public static class MetadataSerializer
    {
        public static long Serialize(MemoryStream stream, Dictionary<string, MetadataValue> values)
        {
            var toSerialize = values.Select(kvp => new KeyValue
            {
                Key = HeaderId.EncodeName(kvp.Key),
                Value = kvp.Value
            }).ToArray();

            var start = stream.Position;

            Serializer.Serialize(stream, toSerialize);

            var end = stream.Position;

            return end - start;
        }

        [ProtoContract]
        public class KeyValue
        {
            [ProtoMember(1, IsPacked = true)]
            public byte[] Key { get; set; }

            [ProtoMember(2)]
            public MetadataValue Value { get; set; }
        }
    }
}