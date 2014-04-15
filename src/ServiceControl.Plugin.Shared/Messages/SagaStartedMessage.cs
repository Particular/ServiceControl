namespace ServiceControl.EndpointPlugin.Messages.SagaState
{
    using System;
    using System.Collections.Generic;

    public class SagaStartedMessage
    {
        public SagaStartedMessage()
        {
            ResultingMessages = new List<SagaChangeResultingMessage>();
        }

        public string SagaState { get; set; }
        public Guid SagaId { get; set; }
        public DateTimeOffset ChangeTimestamp { get; set; }
        public SagaChangeInitiator Initiator { get; set; }
        public List<SagaChangeResultingMessage> ResultingMessages { get; set; }
        public string Endpoint { get; set; }
    }
}
