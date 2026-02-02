namespace ServiceControl.Audit.Persistence.MongoDB.Documents
{
    using System.Collections.Generic;
    using global::MongoDB.Bson;
    using global::MongoDB.Bson.Serialization.Attributes;

    class FailedAuditImportDocument
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("messageId")]
        public string MessageId { get; set; }

        [BsonElement("headers")]
        public Dictionary<string, string> Headers { get; set; }

        [BsonElement("body")]
        public byte[] Body { get; set; }

        [BsonElement("exceptionInfo")]
        public string ExceptionInfo { get; set; }
    }
}
