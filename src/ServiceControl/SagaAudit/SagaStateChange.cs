namespace ServiceControl.Operations.SagaState
{
    using System;
    using System.Collections.Generic;

    public class SagaStateChange
    {
        public DateTime StartTime { get; set; }
        public DateTime FinishTime { get; set; }
        public SagaStateChangeStatus Status { get; set; }
        public string StateAfterChange { get; set; }
        public InitiatingMessage InitiatingMessage { get; set; }
        public List<ResultingMessage> OutgoingMessages { get; set; }
        public string Endpoint { get; set; }
        public bool IsNew { get; set; }
    }
}