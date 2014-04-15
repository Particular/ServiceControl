namespace ServiceControl.EndpointPlugin.Messages.SagaState
{
    using System;

    class SagaChangeInitiator
    {
        public string InitiatingMessageId { get; set; }
        public string MessageType { get; set; }
        public bool IsSagaTimeoutMessage { get; set; }
        public DateTime TimeSent { get; set; }
        public string OriginatingMachine { get; set; }
        public string OriginatingEndpoint { get; set; }
        public string Intent { get; set; }
    }

}