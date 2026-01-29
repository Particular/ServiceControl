namespace ServiceControl.Audit.Persistence.MongoDB.Documents
{
    using System;
    using System.Collections.Generic;
    using global::MongoDB.Bson.Serialization.Attributes;
    using ServiceControl.SagaAudit;

    class SagaSnapshotDocument
    {
        [BsonId]
        public string Id { get; set; }

        [BsonElement("sagaId")]
        [BsonGuidRepresentation(global::MongoDB.Bson.GuidRepresentation.Standard)]
        public Guid SagaId { get; set; }

        [BsonElement("sagaType")]
        public string SagaType { get; set; }

        [BsonElement("startTime")]
        public DateTime StartTime { get; set; }

        [BsonElement("finishTime")]
        public DateTime FinishTime { get; set; }

        [BsonElement("status")]
        public SagaStateChangeStatus Status { get; set; }

        [BsonElement("stateAfterChange")]
        public string StateAfterChange { get; set; }

        [BsonElement("initiatingMessage")]
        [BsonIgnoreIfNull]
        public InitiatingMessageDocument InitiatingMessage { get; set; }

        [BsonElement("outgoingMessages")]
        public List<ResultingMessageDocument> OutgoingMessages { get; set; }

        [BsonElement("endpoint")]
        public string Endpoint { get; set; }

        [BsonElement("processedAt")]
        public DateTime ProcessedAt { get; set; }

        [BsonElement("expiresAt")]
        public DateTime ExpiresAt { get; set; }
    }

    class InitiatingMessageDocument
    {
        [BsonElement("messageId")]
        public string MessageId { get; set; }

        [BsonElement("messageType")]
        public string MessageType { get; set; }

        [BsonElement("isSagaTimeoutMessage")]
        public bool IsSagaTimeoutMessage { get; set; }

        [BsonElement("originatingMachine")]
        public string OriginatingMachine { get; set; }

        [BsonElement("originatingEndpoint")]
        public string OriginatingEndpoint { get; set; }

        [BsonElement("timeSent")]
        public DateTime TimeSent { get; set; }

        [BsonElement("intent")]
        public string Intent { get; set; }
    }

    class ResultingMessageDocument
    {
        [BsonElement("messageId")]
        public string MessageId { get; set; }

        [BsonElement("messageType")]
        public string MessageType { get; set; }

        [BsonElement("destination")]
        public string Destination { get; set; }

        [BsonElement("timeSent")]
        public DateTime TimeSent { get; set; }

        [BsonElement("intent")]
        public string Intent { get; set; }

        [BsonElement("deliveryDelay")]
        [BsonIgnoreIfNull]
        public string DeliveryDelay { get; set; }

        [BsonElement("deliverAt")]
        [BsonIgnoreIfNull]
        public DateTime? DeliverAt { get; set; }
    }
}
