namespace ServiceControl.Audit.Persistence.MongoDB.Documents
{
    using System;
    using global::MongoDB.Bson.Serialization.Attributes;

    class KnownEndpointDocument
    {
        [BsonId]
        public string Id { get; set; }

        [BsonElement("name")]
        public string Name { get; set; }

        [BsonElement("hostId")]
        [BsonGuidRepresentation(global::MongoDB.Bson.GuidRepresentation.Standard)]
        public Guid HostId { get; set; }

        [BsonElement("host")]
        public string Host { get; set; }

        [BsonElement("lastSeen")]
        public DateTime LastSeen { get; set; }

        [BsonElement("expiresAt")]
        public DateTime ExpiresAt { get; set; }
    }
}
