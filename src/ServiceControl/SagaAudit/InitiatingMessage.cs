namespace ServiceControl.Operations.SagaState
{

    public class InitiatingMessage
    {
        public string InitiatingMessageId { get; set; }
        public bool IsSagaTimeoutMessage { get; set; }
        public string OriginatingEndpoint { get; set; }
        public string OriginatingMachine { get; set; }
        //public string ProcessingEnded { get; set; }
        //public string ProcessingStarted { get; set; }
        public string TimeSent { get; set; }
        public string MessageType { get; set; }
    }
}