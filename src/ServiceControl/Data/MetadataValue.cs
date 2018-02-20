namespace Particular.ServiceControl.Data
{
    using System;
    using global::ServiceControl.Contracts.Operations;
    using global::ServiceControl.SagaAudit;
    using ProtoBuf;

    /// <summary>
    /// This class provides a data structure for a distriminated union of metadata values.
    /// </summary>
    /// <remarks>
    /// Using protobuf with low tag number introduce just 1 byte of overhead for writing the value, making it possible to write a union with:
    /// - <see cref="Bool"/> to be written on 2 bytes
    /// - small <see cref="Int"/> to be written on 2 bytes
    /// 
    /// </remarks>
    [ProtoContract]
    public class MetadataValue
    {
        [ProtoMember(1)] public string String { get; set; }
        [ProtoMember(2)] public int Int { get; set; }
        [ProtoMember(3)] public bool Bool { get; set; }
        [ProtoMember(4)] public EndpointDetails EndpointDetails { get; set; }
        [ProtoMember(5)] public DateTime DateTime { get; set; }
        [ProtoMember(6)] public TimeSpan TimeSpan { get; set; }

        [ProtoMember(7)] public SagaInfo SagaInfo { get; set; }

        public static implicit operator MetadataValue(string value)
        {
            return new MetadataValue
            {
                String = value
            };
        }

        public static implicit operator MetadataValue(int value)
        {
            return new MetadataValue
            {
                Int = value
            };
        }

        public static implicit operator MetadataValue(bool value)
        {
            return new MetadataValue
            {
                Bool = value
            };
        }

        public static implicit operator MetadataValue(EndpointDetails value)
        {
            return new MetadataValue
            {
                EndpointDetails = value
            };
        }

        public static implicit operator MetadataValue(DateTime value)
        {
            return new MetadataValue
            {
                DateTime = value
            };
        }

        public static implicit operator MetadataValue(TimeSpan value)
        {
            return new MetadataValue
            {
                TimeSpan = value
            };
        }

        public static implicit operator MetadataValue(SagaInfo value)
        {
            return new MetadataValue
            {
                SagaInfo = value
            };
        }
    }
}