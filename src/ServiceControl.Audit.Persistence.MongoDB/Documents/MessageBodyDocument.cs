namespace ServiceControl.Audit.Persistence.MongoDB.Documents
{
    using global::MongoDB.Bson.Serialization.Attributes;

    class MessageBodyDocument
    {
        [BsonId]
        public string Id { get; set; }

        [BsonElement("contentType")]
        public string ContentType { get; set; }

        [BsonElement("bodySize")]
        public int BodySize { get; set; }

        [BsonElement("body")]
        public string Body { get; set; }
    }
}
