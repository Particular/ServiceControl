namespace Particular.Backend.Debugging
{
    using System;
    using System.Collections.Generic;
    using ServiceControl.Contracts.Operations;

    public class AuditMessageSnapshot
    {
        public AuditMessageSnapshot()
        {
            MessageMetadata = new Dictionary<string, object>();
            Headers = new Dictionary<string, string>();
        }

        public void Initialize(string uniqueId, MessageStatus initialStatus)
        {
            UniqueMessageId = uniqueId;
            Status = initialStatus;
        }

        public MessageStatus Status { get; set; }
        public string UniqueMessageId { get; protected set; }
        public Dictionary<string, object> MessageMetadata { get; protected set; }
        public Dictionary<string, string> Headers { get; protected set; }
        public DateTime AttemptedAt { get; set; }
    }
}