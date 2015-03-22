namespace Particular.Backend.Debugging.RavenDB.Model
{
    using System;
    using System.Collections.Generic;
    using Particular.Backend.Debugging.Api;

    public class SagaSnapshot
    {
        public SagaSnapshot()
        {
            OutgoingMessages = new List<ResultingMessage>();
        }

        public Guid Id { get; set; }
        public Guid SagaId { get; set; }
        public string SagaType { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime FinishTime { get; set; }
        public SagaStateChangeStatus Status { get; set; }
        public string StateAfterChange { get; set; }
        public InitiatingMessage InitiatingMessage { get; set; }
        public List<ResultingMessage> OutgoingMessages { get; set; }
        public string Endpoint { get; set; }
        public DateTime ProcessedAt { get; set; }
    }


}