namespace ServiceControl.EndpointPlugin.Messages.SagaState
{
    using System;
    using System.Collections.Generic;

    public class SagaUpdatedMessage
    {
        public SagaUpdatedMessage()
        {
            ResultingMessages = new List<SagaChangeOutput>();
        }

        public string SagaState { get; set; }
        public Guid SagaId { get; set; }
        public SagaChangeInitiator Initiator { get; set; }
        public List<SagaChangeOutput> ResultingMessages { get; set; }
        public string Endpoint { get; set; }
        public bool IsNew { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime FinishTime { get; set; }
    }
}
