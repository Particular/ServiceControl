namespace ServiceControl.EndpointPlugin.Messages.SagaState
{

    public class SagaChangeInitiator
    {
        public string InitiatingMessageId { get; set; }
        public string MessageType { get; set; }
        //TODO: how to get is timeout?
        public bool IsSagaTimeoutMessage { get; set; }
        public string TimeSent { get; set; }
        public string OriginatingMachine { get; set; }
        public string OriginatingEndpoint { get; set; }
    }

}