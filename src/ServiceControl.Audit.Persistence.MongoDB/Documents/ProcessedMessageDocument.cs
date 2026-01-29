namespace ServiceControl.Audit.Persistence.MongoDB.Documents
{
    using System;
    using System.Collections.Generic;
    using global::MongoDB.Bson;
    using global::MongoDB.Bson.Serialization.Attributes;

    class ProcessedMessageDocument
    {
        [BsonId]
        public string Id { get; set; }

        [BsonElement("uniqueMessageId")]
        public string UniqueMessageId { get; set; }

        [BsonElement("messageMetadata")]
        public BsonDocument MessageMetadata { get; set; }

        [BsonElement("headers")]
        public Dictionary<string, string> Headers { get; set; }

        [BsonElement("body")]
        [BsonIgnoreIfNull]
        public string Body { get; set; }

        [BsonElement("processedAt")]
        public DateTime ProcessedAt { get; set; }

        [BsonElement("expiresAt")]
        public DateTime ExpiresAt { get; set; }
    }
}
