namespace ServiceControl.EndpointPlugin.Messages.SagaState
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;

    public class SagaUpdatedMessage:IMessage
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
        public bool IsCompleted { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime FinishTime { get; set; }
        public string SagaType { get; set; }
    }
}
